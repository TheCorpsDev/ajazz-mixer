using System.Runtime.InteropServices;

namespace OpenDeckMixer;

/// <summary>
/// Simula i tasti multimediali del volume, come una tastiera. È l'unico modo
/// supportato per far comparire il popup del volume di Windows: le API audio
/// cambiano il volume senza mostrarlo. Ogni pressione vale il 2% (fisso, deciso da Windows).
/// </summary>
public static partial class MediaKeys
{
    public const int StepPercent = 2;

    private const ushort VolumeMute = 0xAD;
    private const ushort VolumeDown = 0xAE;
    private const ushort VolumeUp = 0xAF;

    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;

    /// <summary>Cambia il volume di circa <paramref name="percent"/> punti (arrotondato a multipli del 2%).</summary>
    public static void ChangeVolume(int percent)
    {
        if (percent == 0) return;
        var presses = Math.Max(1, (int)Math.Round(Math.Abs(percent) / (double)StepPercent));
        var key = percent > 0 ? VolumeUp : VolumeDown;
        var inputs = new List<Input>(presses * 2);
        for (var i = 0; i < presses; i++)
        {
            inputs.Add(Key(key, up: false));
            inputs.Add(Key(key, up: true));
        }
        Send(inputs);
    }

    public static void ToggleMute() => Send([Key(VolumeMute, up: false), Key(VolumeMute, up: true)]);

    private static Input Key(ushort vk, bool up) => new()
    {
        Type = InputKeyboard,
        Keyboard = new KeyboardInput
        {
            VirtualKey = vk,
            Flags = KeyEventExtendedKey | (up ? KeyEventKeyUp : 0),
        },
    };

    private static void Send(List<Input> inputs)
    {
        var array = inputs.ToArray();
        var sent = SendInput((uint)array.Length, array, Marshal.SizeOf<Input>());
        if (sent != array.Length)
            throw new InvalidOperationException($"SendInput ha inviato {sent} eventi su {array.Length} (errore {Marshal.GetLastPInvokeError()})");
    }

    // INPUT di Win32: l'unione va dimensionata sul membro più grande (MOUSEINPUT),
    // altrimenti cbSize non corrisponde e SendInput rifiuta tutto.
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
        // Riempimento fino alla dimensione di MOUSEINPUT (32 byte su x64).
        private readonly uint _padding1;
        private readonly uint _padding2;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint count, [In] Input[] inputs, int size);
}
