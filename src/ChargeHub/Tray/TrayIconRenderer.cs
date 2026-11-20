using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ChargeHub.Tray;

/// <summary>An <see cref="Icon"/> whose native handle is destroyed on dispose (GDI handles are not garbage collected).</summary>
internal sealed class OwnedIcon : IDisposable
{
    readonly IntPtr handle;

    public OwnedIcon(IntPtr handle)
    {
        this.handle = handle;
        Icon = Icon.FromHandle(handle);
    }

    public Icon Icon { get; }

    public void Dispose()
    {
        Icon.Dispose();
        DestroyIcon(handle);
    }

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr icon);
}

/// <summary>Draws tray icons directly at the tray's pixel size so they stay crisp.</summary>
internal static class TrayIconRenderer
{
    public static bool TaskbarIsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException) { return false; }
    }

    public static OwnedIcon Render(IconGlyph glyph, int size, bool lightTaskbar)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);
            using var brush = new SolidBrush(ColorFor(glyph.Tone, lightTaskbar));
            if (glyph.Tone == IconTone.Hub) DrawBattery(graphics, brush, size);
            else if (glyph.Label == TrayPlanner.NoValue) DrawDash(graphics, brush, size);
            else DrawLabel(graphics, brush, glyph.Label, size);
        }
        return new OwnedIcon(bitmap.GetHicon());
    }

    static Color ColorFor(IconTone tone, bool light) => tone switch
    {
        IconTone.Low => light ? Color.FromArgb(200, 30, 30) : Color.FromArgb(255, 90, 90),
        IconTone.Charging => light ? Color.FromArgb(20, 140, 60) : Color.FromArgb(70, 215, 120),
        IconTone.Muted => light ? Color.FromArgb(110, 110, 110) : Color.FromArgb(150, 150, 150),
        _ => light ? Color.FromArgb(25, 25, 25) : Color.White,
    };

    static void DrawLabel(Graphics graphics, Brush brush, string label, int size)
    {
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        float fontSize = size * (label.Length >= 3 ? 0.58f : 0.8f);
        while (true)
        {
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            if (graphics.MeasureString(label, font, size * 2, format).Width <= size || fontSize <= 6)
            {
                graphics.DrawString(label, font, brush, new RectangleF(0, 0, size, size), format);
                return;
            }
            fontSize -= 0.5f;
        }
    }

    // A drawn bar rather than a text dash: its weight and position do not depend on the font.
    static void DrawDash(Graphics graphics, Brush brush, int size)
    {
        float thickness = Math.Max(2f, size / 8f), width = size * 0.5f;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.FillRectangle(brush, (size - width) / 2f, (size - thickness) / 2f, width, thickness);
    }

    static void DrawBattery(Graphics graphics, Brush brush, int size)
    {
        float unit = size / 16f;
        using var pen = new Pen(brush, Math.Max(1f, unit * 1.2f));
        var body = new RectangleF(1.5f * unit, 4.5f * unit, 11f * unit, 7f * unit);
        graphics.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
        graphics.FillRectangle(brush, 13f * unit, 6.5f * unit, 1.5f * unit, 3f * unit);
        graphics.FillRectangle(brush, 3.5f * unit, 6.5f * unit, 6f * unit, 3f * unit);
    }
}
