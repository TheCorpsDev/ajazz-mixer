using System.Text.Json.Nodes;

namespace OpenDeckMixer;

public static class Format
{
    public static string Percent(float volume) => $"{(int)MathF.Round(volume * 100f)}%";

    /// <summary>"Discord.exe" → "Discord": in Windows il nome processo non ha estensione.</summary>
    public static string NormalizeProcess(string name)
    {
        name = name.Trim();
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    /// <summary>"Spotify, chrome; msedge" → ["Spotify", "chrome", "msedge"]</summary>
    public static List<string> ParseApps(string? value) =>
        (value ?? "")
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => NormalizeProcess(s).Length > 0)
            .ToList();

    /// <summary>Passo per scatto della manopola, in percentuale (1–20, default 2).</summary>
    public static int Step(JsonObject settings)
    {
        var node = settings["step"];
        int step;
        try
        {
            step = node switch
            {
                JsonValue v when v.TryGetValue<int>(out var i) => i,
                JsonValue v when v.TryGetValue<double>(out var d) => (int)Math.Round(d),
                JsonValue v when v.TryGetValue<string>(out var s) && int.TryParse(s, out var p) => p,
                _ => 2,
            };
        }
        catch
        {
            step = 2;
        }
        return Math.Clamp(step, 1, 20);
    }

    /// <summary>Mostrare il popup del volume di Windows (solo master, default sì).</summary>
    public static bool Popup(JsonObject settings) =>
        settings["popup"] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : true;

    public static string Title(VolumeState state) => state.Muted ? "Muto" : Percent(state.Volume);
}
