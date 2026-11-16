using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace OpenDeckMixer;

/// <summary>
/// Popup del volume per le singole app, in stile Windows 11: compare in basso al
/// centro con l'icona dell'app, non ruba il focus, non intercetta i clic e sparisce dopo poco.
/// Gira su un thread UI dedicato; <see cref="Show"/> si può chiamare da qualsiasi thread.
/// </summary>
public static class Osd
{
    private static readonly object Gate = new();
    private static OsdWindow? _window;

    /// <summary>Quanto resta visibile prima della dissolvenza (modificabile per la diagnostica).</summary>
    public static int VisibleMs { get; set; } = 1500;

    public static void Show(string name, VolumeState? state, uint processId)
    {
        var window = EnsureStarted();
        if (window is null) return;
        try
        {
            window.BeginInvoke(() => window.Display(name, state, processId));
        }
        catch (Exception e)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] popup non disponibile: {e.Message}");
        }
    }

    private static OsdWindow? EnsureStarted()
    {
        lock (Gate)
        {
            if (_window is not null) return _window;
            using var ready = new ManualResetEventSlim();
            OsdWindow? created = null;
            var thread = new Thread(() =>
            {
                try
                {
                    Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                    created = new OsdWindow();
                    _ = created.Handle; // crea la finestra su questo thread
                }
                finally
                {
                    ready.Set();
                }
                Application.Run();
            })
            {
                IsBackground = true,
                Name = "Popup volume",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait(TimeSpan.FromSeconds(5));
            _window = created;
            return _window;
        }
    }
}

internal sealed class OsdWindow : Form
{
    private const int FadeStepMs = 15;
    private const int FadeStep = 32;

    private readonly System.Windows.Forms.Timer _hideTimer = new();
    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = FadeStepMs };
    private int _alpha;
    private bool _shown;

    public OsdWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            _fadeTimer.Start();
        };
        _fadeTimer.Tick += (_, _) =>
        {
            _alpha = Math.Max(0, _alpha - FadeStep);
            Native.UpdateLayeredAlpha(Handle, (byte)_alpha);
            if (_alpha > 0) return;
            _fadeTimer.Stop();
            Native.ShowWindow(Handle, Native.SW_HIDE);
            _shown = false;
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW
                          | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
            return cp;
        }
    }

    public void Display(string name, VolumeState? state, uint processId)
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1040);
        var scale = Native.DpiScaleAt(new Point(area.Left + area.Width / 2, area.Top + area.Height / 2));
        var icon = AppIcons.Get(processId, (int)Math.Round(OsdRenderer.IconSize * scale));

        using var bitmap = OsdRenderer.Render(name, state, icon, scale);
        var position = new Point(
            area.Left + (area.Width - bitmap.Width) / 2,
            area.Bottom - bitmap.Height - (int)Math.Round(24 * scale));

        _fadeTimer.Stop();
        _alpha = 255;
        Native.UpdateLayered(Handle, bitmap, position, 255);
        if (!_shown)
        {
            Native.ShowWindow(Handle, Native.SW_SHOWNOACTIVATE);
            _shown = true;
        }
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);

        _hideTimer.Stop();
        _hideTimer.Interval = Math.Max(100, Osd.VisibleMs);
        _hideTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hideTimer.Dispose();
            _fadeTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>Disegna il popup su una bitmap con trasparenza per pixel.</summary>
internal static class OsdRenderer
{
    public const int IconSize = 36;

    private static readonly string IconFont = PickIconFont();

    public static Bitmap Render(string name, VolumeState? state, Bitmap? icon, float scale)
    {
        int S(float v) => (int)Math.Round(v * scale);

        var theme = Theme.Current();
        var width = S(340);
        var height = S(76);
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);

        // Sfondo arrotondato con bordo sottile
        var card = new RectangleF(0.5f, 0.5f, width - 1, height - 1);
        using (var path = RoundedRect(card, S(8)))
        {
            using var fill = new SolidBrush(theme.Background);
            using var border = new Pen(theme.Border, Math.Max(1, scale));
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        var padding = S(16);
        var volume = state?.Volume ?? 0f;
        var muted = state?.Muted ?? false;
        var silent = muted || state is null;

        // Icona dell'app a sinistra (attenuata se muta); senza icona, l'altoparlante
        var iconSize = S(IconSize);
        var iconBox = new Rectangle(padding, (height - iconSize) / 2, iconSize, iconSize);
        if (icon is not null)
        {
            if (silent)
            {
                using var attributes = new ImageAttributes();
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = 0.45f });
                g.DrawImage(icon, iconBox, 0, 0, icon.Width, icon.Height, GraphicsUnit.Pixel, attributes);
            }
            else
            {
                g.DrawImage(icon, iconBox);
            }
        }
        else
        {
            using var glyphFont = new Font(IconFont, 24f * scale, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(theme.Text);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            var glyph = silent ? "" : volume <= 0f ? "" : volume < 0.34f ? "" : volume < 0.67f ? "" : "";
            g.DrawString(glyph, glyphFont, brush, iconBox, format);
        }

        // Con l'icona dell'app, il muto si vede da un piccolo badge in basso a destra
        if (icon is not null && muted)
        {
            var badge = S(18);
            var badgeRect = new RectangleF(iconBox.Right - badge + S(4), iconBox.Bottom - badge + S(4), badge, badge);
            using var badgeFill = new SolidBrush(theme.BadgeBackground);
            g.FillEllipse(badgeFill, badgeRect);
            using var glyphFont = new Font(IconFont, 11f * scale, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(theme.Text);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("", glyphFont, brush, badgeRect, format);
        }

        var contentLeft = iconBox.Right + S(14);

        // Riga 1: nome dell'app (e stato se non sta usando l'audio)
        using (var font = new Font("Segoe UI Semibold", 13f * scale, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(theme.Text))
        {
            var label = state is null ? $"{name} · nessun audio" : name;
            var rect = new RectangleF(contentLeft, S(12), width - contentLeft - padding, S(22));
            using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            g.DrawString(label, font, brush, rect, format);
        }

        // Riga 2: barra e percentuale
        var rowTop = S(38);
        var rowHeight = S(26);
        var percentWidth = S(44);
        var barLeft = contentLeft + S(2);
        var barRight = width - padding - percentWidth - S(8);
        var barHeight = S(4);
        var barTop = rowTop + (rowHeight - barHeight) / 2f;

        using (var track = new SolidBrush(theme.Track))
        using (var trackPath = RoundedRect(new RectangleF(barLeft, barTop, barRight - barLeft, barHeight), barHeight / 2f))
            g.FillPath(track, trackPath);

        if (state is not null)
        {
            var fillWidth = Math.Max(barHeight, (barRight - barLeft) * volume);
            var barColor = muted ? theme.Track : theme.Accent;
            using var accent = new SolidBrush(barColor);
            if (volume > 0f)
            {
                using var fillPath = RoundedRect(new RectangleF(barLeft, barTop, fillWidth, barHeight), barHeight / 2f);
                g.FillPath(accent, fillPath);
            }
            // Pomello alla fine della barra, come lo slider di Windows
            var knob = S(14);
            var cx = barLeft + (volume > 0f ? fillWidth : 0f);
            using var knobFill = new SolidBrush(theme.KnobRing);
            g.FillEllipse(knobFill, cx - knob / 2f, barTop + barHeight / 2f - knob / 2f, knob, knob);
            var inner = S(8);
            using var innerFill = new SolidBrush(muted ? theme.Text : theme.Accent);
            g.FillEllipse(innerFill, cx - inner / 2f, barTop + barHeight / 2f - inner / 2f, inner, inner);
        }

        using (var font = new Font("Segoe UI", 15f * scale, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(theme.Text))
        {
            var text = state is null ? "—" : muted ? "Muto" : ((int)MathF.Round(volume * 100f)).ToString();
            using var format = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, brush, new RectangleF(width - padding - percentWidth - S(10), rowTop, percentWidth + S(10), rowHeight), format);
        }

        return bitmap;
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static string PickIconFont()
    {
        using var fonts = new InstalledFontCollection();
        var names = fonts.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return names.Contains("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
    }
}

/// <summary>Colori presi dal tema di Windows (chiaro/scuro e colore principale).</summary>
internal readonly record struct Theme(Color Background, Color Border, Color Text, Color Track, Color Accent, Color KnobRing, Color BadgeBackground)
{
    public static Theme Current()
    {
        var light = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = key?.GetValue("SystemUsesLightTheme") is int v && v == 1;
        }
        catch
        {
            // tema scuro di default
        }

        var accent = Native.AccentColor() ?? Color.FromArgb(0x4C, 0xC2, 0xFF);
        return light
            ? new Theme(Color.FromArgb(250, 0xF9, 0xF9, 0xF9), Color.FromArgb(40, 0, 0, 0), Color.FromArgb(0x1A, 0x1A, 0x1A),
                Color.FromArgb(70, 0, 0, 0), accent, Color.White, Color.FromArgb(0xE5, 0xE5, 0xE5))
            : new Theme(Color.FromArgb(250, 0x2B, 0x2B, 0x2B), Color.FromArgb(45, 255, 255, 255), Color.White,
                Color.FromArgb(80, 255, 255, 255), accent, Color.FromArgb(0x45, 0x45, 0x45), Color.FromArgb(0x3A, 0x3A, 0x3A));
    }
}

internal static class Native
{
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOPMOST = 0x00000008;

    public const int SW_HIDE = 0;
    public const int SW_SHOWNOACTIVATE = 4;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;

    private const int ULW_ALPHA = 2;
    private const byte AC_SRC_OVER = 0;
    private const byte AC_SRC_ALPHA = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize(int cx, int cy)
    {
        public int Cx = cx;
        public int Cy = cy;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    /// <summary>Disegna la bitmap (alpha per pixel) come contenuto della finestra.</summary>
    public static void UpdateLayered(IntPtr hwnd, Bitmap bitmap, Point position, byte alpha)
    {
        var screenDc = GetDC(IntPtr.Zero);
        var memDc = CreateCompatibleDC(screenDc);
        var hBitmap = ToPremultipliedDib(screenDc, bitmap);
        var old = SelectObject(memDc, hBitmap);
        try
        {
            var dst = new NativePoint(position.X, position.Y);
            var size = new NativeSize(bitmap.Width, bitmap.Height);
            var src = new NativePoint(0, 0);
            var blend = new BlendFunction { BlendOp = AC_SRC_OVER, SourceConstantAlpha = alpha, AlphaFormat = AC_SRC_ALPHA };
            UpdateLayeredWindow(hwnd, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            SelectObject(memDc, old);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>
    /// UpdateLayeredWindow vuole pixel BGRA premoltiplicati. Bitmap.GetHbitmap non li
    /// produce in modo affidabile (lo sfondo risultava semitrasparente), quindi copio
    /// i pixel a mano in una DIB section 32 bit, chiedendo a GDI+ il formato PArgb.
    /// </summary>
    private static unsafe IntPtr ToPremultipliedDib(IntPtr screenDc, Bitmap bitmap)
    {
        var header = new BitmapInfoHeader
        {
            Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
            Width = bitmap.Width,
            Height = -bitmap.Height, // dall'alto verso il basso, come le righe di GDI+
            Planes = 1,
            BitCount = 32,
        };
        var hBitmap = CreateDIBSection(screenDc, ref header, 0, out var bits, IntPtr.Zero, 0);
        if (hBitmap == IntPtr.Zero) throw new InvalidOperationException("CreateDIBSection non riuscita");

        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        try
        {
            var rowBytes = bitmap.Width * 4;
            for (var y = 0; y < bitmap.Height; y++)
            {
                Buffer.MemoryCopy(
                    (byte*)data.Scan0 + (long)y * data.Stride,
                    (byte*)bits + (long)y * rowBytes,
                    rowBytes, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return hBitmap;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfoHeader header, uint usage, out IntPtr bits, IntPtr section, uint offset);

    /// <summary>Cambia solo la trasparenza complessiva (per la dissolvenza).</summary>
    public static void UpdateLayeredAlpha(IntPtr hwnd, byte alpha)
    {
        var blend = new BlendFunction { BlendOp = AC_SRC_OVER, SourceConstantAlpha = alpha, AlphaFormat = AC_SRC_ALPHA };
        UpdateLayeredWindowAlpha(hwnd, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref blend, ULW_ALPHA);
    }

    public static float DpiScaleAt(Point point)
    {
        try
        {
            var monitor = MonitorFromPoint(new NativePoint(point.X, point.Y), 2 /* MONITOR_DEFAULTTONEAREST */);
            if (GetDpiForMonitor(monitor, 0 /* MDT_EFFECTIVE_DPI */, out var dpiX, out _) == 0 && dpiX > 0)
                return dpiX / 96f;
        }
        catch
        {
            // Windows molto vecchi: scala 1
        }
        return 1f;
    }

    public static Color? AccentColor()
    {
        try
        {
            // Colore principale scelto in Impostazioni → Personalizzazione → Colori (formato ABGR)
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
            {
                var c = Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
                return c;
            }
        }
        catch
        {
            // colore di default
        }
        return null;
    }

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hwnd, int cmd);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref NativePoint pptDst, ref NativeSize psize,
        IntPtr hdcSrc, ref NativePoint pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);

    [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow", SetLastError = true)]
    private static extern bool UpdateLayeredWindowAlpha(IntPtr hwnd, IntPtr hdcDst, IntPtr pptDst, IntPtr psize,
        IntPtr hdcSrc, IntPtr pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint pt, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);
}

/// <summary>
/// Icona dell'app ricavata dal suo eseguibile (come in Esplora file), con
/// trasparenza. Messa in cache per percorso e dimensione. Da usare sul thread UI (STA).
/// </summary>
internal static class AppIcons
{
    private static readonly Dictionary<(string Path, int Size), Bitmap?> Cache = new();

    public static Bitmap? Get(uint processId, int size)
    {
        if (processId == 0) return null;
        var path = ExecutablePath(processId);
        if (path is null) return null;
        if (Cache.TryGetValue((path, size), out var cached)) return cached;
        var icon = FromShell(path, size) ?? FromAssociatedIcon(path);
        Cache[(path, size)] = icon;
        return icon;
    }

    private static string? ExecutablePath(uint processId)
    {
        var handle = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, processId);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var buffer = new char[1024];
            var length = buffer.Length;
            return QueryFullProcessImageName(handle, 0, buffer, ref length) ? new string(buffer, 0, length) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>Icona ad alta risoluzione tramite la shell di Windows.</summary>
    private static Bitmap? FromShell(string path, int size)
    {
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory);
            const int SIIGBF_BIGGERSIZEOK = 0x1, SIIGBF_ICONONLY = 0x4;
            if (factory.GetImage(new IconSize(size, size), SIIGBF_BIGGERSIZEOK | SIIGBF_ICONONLY, out hBitmap) != 0 || hBitmap == IntPtr.Zero)
                return null;
            return WithAlpha(hBitmap);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
        }
    }

    // Image.FromHbitmap perde il canale alpha: rileggo i pixel come ARGB premoltiplicato.
    private static Bitmap WithAlpha(IntPtr hBitmap)
    {
        using var source = Image.FromHbitmap(hBitmap);
        var bounds = new Rectangle(0, 0, source.Width, source.Height);
        var data = source.LockBits(bounds, ImageLockMode.ReadOnly, source.PixelFormat);
        try
        {
            if (Image.GetPixelFormatSize(source.PixelFormat) < 32) return new Bitmap(source);
            using var view = new Bitmap(data.Width, data.Height, data.Stride, PixelFormat.Format32bppPArgb, data.Scan0);
            return view.Clone(bounds, PixelFormat.Format32bppArgb);
        }
        finally
        {
            source.UnlockBits(data);
        }
    }

    private static Bitmap? FromAssociatedIcon(string path)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            return icon?.ToBitmap();
        }
        catch
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconSize(int cx, int cy)
    {
        public int Cx = cx;
        public int Cy = cy;
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(IconSize size, int flags, out IntPtr phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, [Out] char[] buffer, ref int size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);
}
