using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace OpenDeckMixer;

public readonly record struct VolumeState(float Volume, bool Muted);

public readonly record struct AppVolume(string Name, VolumeState State);

/// <summary>
/// Accesso al mixer di Windows (Core Audio): volume master del dispositivo
/// predefinito e volume delle singole app, cercate per nome del processo
/// su tutti i dispositivi di uscita attivi.
/// Va usato da un solo thread alla volta: il plugin serializza le chiamate.
/// </summary>
public static class AudioMixer
{
    private static readonly MMDeviceEnumerator Enumerator = new();

    // ── Master ───────────────────────────────────────────────────────────

    public static VolumeState? GetMaster() =>
        WithDefaultDevice(v => new VolumeState(v.MasterVolumeLevelScalar, v.Mute));

    public static VolumeState? ChangeMaster(float delta) =>
        WithDefaultDevice(v =>
        {
            var volume = Clamp(v.MasterVolumeLevelScalar + delta);
            v.MasterVolumeLevelScalar = volume;
            // Alzare il volume da muto toglie il muto, come fa Windows.
            if (delta > 0 && v.Mute) v.Mute = false;
            return new VolumeState(volume, v.Mute);
        });

    public static VolumeState? ToggleMasterMute() =>
        WithDefaultDevice(v =>
        {
            v.Mute = !v.Mute;
            return new VolumeState(v.MasterVolumeLevelScalar, v.Mute);
        });

    private static VolumeState? WithDefaultDevice(Func<AudioEndpointVolume, VolumeState> action)
    {
        if (!Enumerator.TryGetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var device) || device is null)
            return null;
        using (device)
            return action(device.AudioEndpointVolume);
    }

    // ── App ─────────────────────────────────────────────────────────────

    /// <summary>Stato della prima app dell'elenco che ha una sessione audio.</summary>
    public static AppVolume? GetApp(IReadOnlyList<string> apps) =>
        WithFirstApp(apps, (name, sessions) => new AppVolume(name, StateOf(sessions)));

    public static AppVolume? ChangeApp(IReadOnlyList<string> apps, float delta) =>
        WithFirstApp(apps, (name, sessions) =>
        {
            var current = StateOf(sessions);
            var volume = Clamp(current.Volume + delta);
            var unmute = delta > 0 && current.Muted;
            foreach (var s in sessions)
            {
                s.SimpleAudioVolume.Volume = volume;
                if (unmute) s.SimpleAudioVolume.Mute = false;
            }
            return new AppVolume(name, new VolumeState(volume, current.Muted && !unmute));
        });

    public static AppVolume? ToggleAppMute(IReadOnlyList<string> apps) =>
        WithFirstApp(apps, (name, sessions) =>
        {
            var current = StateOf(sessions);
            var mute = !current.Muted;
            foreach (var s in sessions) s.SimpleAudioVolume.Mute = mute;
            return new AppVolume(name, current with { Muted = mute });
        });

    /// <summary>Tutte le app che hanno almeno una sessione audio, senza duplicati.</summary>
    public static List<AppVolume> ListApps()
    {
        var result = new List<AppVolume>();
        WithAllSessions(all =>
        {
            foreach (var group in all.GroupBy(x => x.Process, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                result.Add(new AppVolume(group.First().Process, StateOf(group.Select(x => x.Session).ToList())));
        });
        return result;
    }

    // Lo stato di un'app con più sessioni (es. browser) è quello della prima;
    // risulta muta solo se lo sono tutte.
    private static VolumeState StateOf(List<AudioSessionControl> sessions) =>
        new(sessions[0].SimpleAudioVolume.Volume, sessions.All(s => s.SimpleAudioVolume.Mute));

    private static T? WithFirstApp<T>(IReadOnlyList<string> apps, Func<string, List<AudioSessionControl>, T> action)
        where T : struct
    {
        T? result = null;
        WithAllSessions(all =>
        {
            foreach (var app in apps)
            {
                var wanted = Format.NormalizeProcess(app);
                var sessions = all.Where(x => string.Equals(Format.NormalizeProcess(x.Process), wanted, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Session)
                    .ToList();
                if (sessions.Count == 0) continue;
                result = action(app.Trim(), sessions);
                return;
            }
        });
        return result;
    }

    /// <summary>
    /// Raccoglie le sessioni audio di tutti i dispositivi di uscita attivi
    /// (non solo quello predefinito: Discord ad esempio può uscire sulle cuffie),
    /// esegue l'azione e rilascia tutti gli oggetti COM.
    /// </summary>
    private static void WithAllSessions(Action<List<(string Process, AudioSessionControl Session)>> action)
    {
        var devices = new List<MMDevice>();
        var sessions = new List<(string Process, AudioSessionControl Session)>();
        try
        {
            var collection = Enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            for (var i = 0; i < collection.Count; i++)
            {
                var device = collection[i];
                devices.Add(device);
                var manager = device.AudioSessionManager;
                manager.RefreshSessions();
                var list = manager.Sessions;
                for (var j = 0; j < list.Count; j++)
                {
                    var session = list[j];
                    var name = session.IsSystemSoundsSession ? "" : ProcessNames.Get(session.GetProcessID);
                    if (name.Length == 0 || session.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateExpired)
                    {
                        session.Dispose();
                        continue;
                    }
                    sessions.Add((name, session));
                }
            }
            action(sessions);
        }
        finally
        {
            foreach (var (_, session) in sessions) session.Dispose();
            foreach (var device in devices) device.Dispose();
        }
    }

    private static float Clamp(float v) => Math.Clamp(MathF.Round(v * 100f) / 100f, 0f, 1f);

    /// <summary>
    /// Cache PID → nome processo: risalire al nome è relativamente lento e la
    /// manopola genera molti eventi al secondo. Svuotata ogni pochi secondi
    /// per non confondersi se un PID viene riutilizzato.
    /// </summary>
    private static class ProcessNames
    {
        private static readonly Dictionary<uint, string> Cache = new();
        private static long _resetAt;

        public static string Get(uint pid)
        {
            if (pid == 0) return "";
            var now = Environment.TickCount64;
            if (now >= _resetAt)
            {
                Cache.Clear();
                _resetAt = now + 5000;
            }
            if (Cache.TryGetValue(pid, out var name)) return name;
            try
            {
                using var p = Process.GetProcessById((int)pid);
                name = p.ProcessName;
            }
            catch
            {
                name = "";
            }
            Cache[pid] = name;
            return name;
        }
    }
}
