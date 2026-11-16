using System.Runtime.Versioning;
using OpenDeckMixer;

[assembly: SupportedOSPlatform("windows")]

// Modalità diagnostica: elenca le app con una sessione audio e il volume master.
// Utile per scoprire il nome esatto del processo da usare nelle impostazioni.
if (args.Contains("--list"))
{
    var master = AudioMixer.GetMaster();
    Console.WriteLine(master is null
        ? "Master: nessun dispositivo di uscita predefinito"
        : $"Master: {Format.Percent(master.Value.Volume)}{(master.Value.Muted ? " (muto)" : "")}");
    foreach (var app in AudioMixer.ListApps())
        Console.WriteLine($"- {app.Name}: {Format.Percent(app.State.Volume)}{(app.State.Muted ? " (muto)" : "")}");
    return 0;
}

// Avvio da OpenDeck: -port <n> -pluginUUID <id> -registerEvent <evento> -info <json>
string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var port = Arg("-port");
var pluginUuid = Arg("-pluginUUID");
var registerEvent = Arg("-registerEvent");
if (port is null || pluginUuid is null || registerEvent is null)
{
    Console.Error.WriteLine("Uso: OpenDeckMixer -port <porta> -pluginUUID <uuid> -registerEvent <evento> -info <json>");
    Console.Error.WriteLine("     OpenDeckMixer --list");
    return 1;
}

var plugin = new Plugin(int.Parse(port), pluginUuid, registerEvent);
await plugin.RunAsync();
return 0;
