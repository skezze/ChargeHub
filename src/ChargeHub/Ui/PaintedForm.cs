using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using ChargeHub.Core;
using ChargeHub.Tray;

namespace ChargeHub.Ui;

/// <summary>
/// Base for the app's windows. They are painted by hand from a list of <see cref="Visual"/>s
/// (no child controls), which keeps them light and gives full control over the look. The base
/// provides painting order, hover/click handling, a scrolling viewport and the shared widgets.
/// </summary>
internal abstract class PaintedForm : Form
{
    protected sealed class Visual(Rectangle bounds, bool scrolls, Action<Graphics, Rectangle, bool> paint, Action? click = null)
    {
        public Rectangle Bounds { get; } = bounds;
        public bool Scrolls { get; } = scrolls;
        public Action<Graphics, Rectangle, bool> Paint { get; } = paint;
        public Action? Click { get; } = click;
    }

    protected readonly List<Visual> Visuals = [];
    protected readonly Font TitleFont = new("Segoe UI Semibold", 13f);
    protected readonly Font HeadingFont = new("Segoe UI Semibold", 20f);
    protected readonly Font NameFont = new("Segoe UI Semibold", 10f);
    protected readonly Font PercentFont = new("Segoe UI Semibold", 15f);
    protected readonly Font BigPercentFont = new("Segoe UI Semibold", 22f);
    protected readonly Font BodyFont = new("Segoe UI", 9.5f);
    protected readonly Font SmallFont = new("Segoe UI", 8.5f);
    static readonly string IconFamily = Fonts.Installed("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
    protected readonly Font IconFont = new(IconFamily, 11f);
    protected readonly Font HugeIconFont = new(IconFamily, 26f);

    protected Theme Theme = Theme.Current();

    /// <summary>Region in which <see cref="Visual.Scrolls"/> visuals are visible. Empty = nothing scrolls.</summary>
    protected Rectangle Viewport;
    protected int ScrollOffset, ContentHeight;
    protected Visual? Hover;

    protected PaintedForm()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected int S(int value) => (int)Math.Round(value * DeviceDpi / 96.0);

    /// <summary>Rebuilds <see cref="Visuals"/> and sizes the window content. Must call <see cref="Invalidate()"/>-safe code only.</summary>
    protected abstract void Relayout();

    protected void FinishLayout()
    {
        ScrollOffset = Math.Clamp(ScrollOffset, 0, Math.Max(0, ContentHeight - Viewport.Height));
        Hover = HitTest(PointToClient(Cursor.Position));
        Invalidate();
    }

    // ---------- window chrome ----------

    protected void ApplyChrome(bool tintCaption)
    {
        // Windows 11: rounded corners, dark mode title bar and caption colour. Silently ignored on older builds.
        int round = 2, dark = Theme.IsLight ? 0 : 1;
        DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        if (tintCaption)
        {
            int caption = Theme.Back.R | (Theme.Back.G << 8) | (Theme.Back.B << 16);
            int text = Theme.Text.R | (Theme.Text.G << 8) | (Theme.Text.B << 16);
            DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
            DwmSetWindowAttribute(Handle, 36, ref text, sizeof(int));
        }
        BackColor = Theme.Back;
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    // ---------- painting ----------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Back);

        // GDI text ignores the clip region, so the scrolling content is drawn first and its overflow
        // is covered with the background before the fixed visuals are painted on top.
        if (!Viewport.IsEmpty)
        {
            foreach (var visual in Visuals.Where(v => v.Scrolls))
            {
                var bounds = Actual(visual);
                if (bounds.IntersectsWith(Viewport)) visual.Paint(g, bounds, visual == Hover);
            }
            using var back = new SolidBrush(Theme.Back);
            g.FillRectangle(back, 0, 0, Width, Viewport.Top);
            g.FillRectangle(back, 0, Viewport.Bottom, Width, Math.Max(0, Height - Viewport.Bottom));
            g.FillRectangle(back, 0, Viewport.Top, Viewport.Left, Viewport.Height);
            g.FillRectangle(back, Viewport.Right, Viewport.Top, Math.Max(0, Width - Viewport.Right), Viewport.Height);
        }
        foreach (var visual in Visuals.Where(v => !v.Scrolls))
            visual.Paint(g, visual.Bounds, visual == Hover);

        if (!Viewport.IsEmpty && ContentHeight > Viewport.Height) // slim scroll indicator
        {
            int track = Viewport.Height - S(8);
            int thumb = Math.Max(S(24), track * Viewport.Height / ContentHeight);
            int top = Viewport.Y + S(4) + (track - thumb) * ScrollOffset / Math.Max(1, ContentHeight - Viewport.Height);
            FillRound(g, Theme.Track, new Rectangle(Viewport.Right - S(6), top, S(3), thumb), S(3));
        }
    }

    protected void PaintDeviceCard(Graphics g, Rectangle r, Device device, bool pinned, bool hot, bool compact, bool large, int lowThreshold)
    {
        FillRound(g, hot ? Theme.CardHover : Theme.Card, r, S(8));
        int left = r.X + S(16), right = r.Right - S(16);
        var tone = TrayPlanner.Glyph(device, lowThreshold).Tone;
        var toneColor = ToneColor(tone);
        bool disconnected = device.Link == LinkStatus.Disconnected;

        // "In tray" switch, bottom-right (vertically centred on compact cards)
        int switchY = compact ? r.Y + (r.Height - S(20)) / 2 : r.Bottom - S(30);
        var toggle = new Rectangle(right - S(40), switchY, S(40), S(20));
        PaintSwitch(g, toggle, pinned);
        var label = new Rectangle(toggle.X - S(8) - S(64), toggle.Y, S(64), toggle.Height);
        DrawText(g, Strings.ShowInTray, SmallFont, label, Theme.Muted, TextFormatFlags.Right);

        int extra = large ? S(6) : 0;
        if (compact)
        {
            DrawText(g, device.Name, NameFont, new Rectangle(left, r.Y + S(10) + extra / 2, label.X - left - S(8), S(22)), disconnected ? Theme.Muted : Theme.Text);
            DrawText(g, Strings.CardSubtitle(device), SmallFont, new Rectangle(left, r.Y + S(32) + extra / 2, label.X - left - S(8), S(18)), Theme.Muted);
            return;
        }

        string percent = device.KnownPercent is int p ? $"{p}%" : "—";
        var percentRect = new Rectangle(right - S(90), r.Y + S(8) + extra / 2, S(90), S(large ? 36 : 30));
        var percentColor = device.IsStale || device.KnownPercent is null ? Theme.Muted : tone == IconTone.Normal ? Theme.Text : toneColor;
        DrawText(g, percent, large ? BigPercentFont : PercentFont, percentRect, percentColor, TextFormatFlags.Right);
        DrawText(g, device.Name, NameFont, new Rectangle(left, r.Y + S(10) + extra, percentRect.X - left - S(8), S(22)), Theme.Text);
        DrawText(g, Strings.CardSubtitle(device), SmallFont, new Rectangle(left, r.Y + S(34) + extra, right - left, S(18)), Theme.Muted);

        var track = new Rectangle(left, r.Bottom - S(23), label.X - left - S(12), S(6));
        FillRound(g, Theme.Track, track, track.Height);
        if (device.KnownPercent is int level && level > 0)
        {
            int fill = Math.Max(track.Height, track.Width * level / 100);
            FillRound(g, toneColor, new Rectangle(track.X, track.Y, fill, track.Height), track.Height);
        }
    }

    protected Color ToneColor(IconTone tone) => tone switch
    {
        IconTone.Low => Theme.Low,
        IconTone.Charging => Theme.Charging,
        IconTone.Muted => Theme.Muted,
        _ => Theme.Accent,
    };

    protected void PaintSwitch(Graphics g, Rectangle r, bool on)
    {
        int knob = r.Height - S(8);
        if (on)
        {
            FillRound(g, Theme.Accent, r, r.Height);
            using var brush = new SolidBrush(Theme.IsLight ? Color.White : Color.FromArgb(24, 24, 24));
            g.FillEllipse(brush, r.Right - S(4) - knob, r.Y + S(4), knob, knob);
        }
        else
        {
            using var path = RoundPath(Rectangle.Inflate(r, -1, -1), r.Height);
            using var pen = new Pen(Theme.Muted, Math.Max(1.5f, DeviceDpi / 96f * 1.5f));
            g.DrawPath(pen, path);
            using var brush = new SolidBrush(Theme.Muted);
            g.FillEllipse(brush, r.X + S(4), r.Y + S(4), knob, knob);
        }
    }

    protected static void DrawText(Graphics g, string text, Font font, Rectangle bounds, Color color, TextFormatFlags align = TextFormatFlags.Left) =>
        TextRenderer.DrawText(g, text, font, bounds, color,
            align | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

    protected static void DrawWrapped(Graphics g, string text, Font font, Rectangle bounds, Color color) =>
        TextRenderer.DrawText(g, text, font, bounds, color,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

    protected static int MeasureHeight(string text, Font font, int width) =>
        TextRenderer.MeasureText(text, font, new Size(Math.Max(1, width), int.MaxValue),
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;

    protected static int MeasureWidth(string text, Font font) =>
        TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;

    protected static void FillRound(Graphics g, Color color, Rectangle r, int radius)
    {
        using var path = RoundPath(r, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    protected static GraphicsPath RoundPath(Rectangle r, int radius)
    {
        int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ---------- input ----------

    Rectangle Actual(Visual visual) =>
        visual.Scrolls ? visual.Bounds with { Y = visual.Bounds.Y + Viewport.Y - ScrollOffset } : visual.Bounds;

    protected Visual? HitTest(Point point)
    {
        // Later visuals are painted on top, so they win.
        for (int i = Visuals.Count - 1; i >= 0; i--)
        {
            var visual = Visuals[i];
            if (visual.Click is null || !Actual(visual).Contains(point)) continue;
            if (visual.Scrolls && !Viewport.Contains(point)) continue;
            return visual;
        }
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTest(e.Location);
        if (hit == Hover) return;
        Hover = hit;
        Cursor = hit is null ? Cursors.Default : Cursors.Hand;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (Hover is null) return;
        Hover = null;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left) HitTest(e.Location)?.Click?.Invoke();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int max = Math.Max(0, ContentHeight - Viewport.Height);
        int next = Math.Clamp(ScrollOffset - e.Delta / 120 * S(56), 0, max);
        if (next == ScrollOffset) return;
        ScrollOffset = next;
        Hover = HitTest(e.Location);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            foreach (var font in new[] { TitleFont, HeadingFont, NameFont, PercentFont, BigPercentFont, BodyFont, SmallFont, IconFont, HugeIconFont }) font.Dispose();
        base.Dispose(disposing);
    }
}

internal static class Fonts
{
    public static bool Installed(string family)
    {
        using var collection = new System.Drawing.Text.InstalledFontCollection();
        return collection.Families.Any(f => f.Name.Equals(family, StringComparison.OrdinalIgnoreCase));
    }
}
