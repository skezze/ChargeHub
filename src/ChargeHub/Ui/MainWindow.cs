using ChargeHub.Settings;
using ChargeHub.Core;
using ChargeHub.Tray;
using ChargeHub.Updates;

namespace ChargeHub.Ui;

/// <summary>
/// The full app window: a sidebar with Devices / Settings / About pages. Closing it only hides it,
/// the app keeps living in the tray. The pages live in the other partial files.
/// </summary>
internal sealed partial class MainWindow : PaintedForm
{
    enum Page { Devices, Settings, About }

    const int SidebarWidth = 220, MaxContentWidth = 760;

    readonly AppSettings settings;
    readonly Func<IReadOnlyList<Device>> devices;
    readonly Func<DateTimeOffset?> lastPoll;
    readonly UpdateService updates;
    readonly Icon? appIcon;
    readonly Bitmap logo;
    Page page = Page.Devices;
    bool startupEnabled;

    public MainWindow(AppSettings settings, Func<IReadOnlyList<Device>> devices, Func<DateTimeOffset?> lastPoll, UpdateService updates)
    {
        this.settings = settings;
        this.devices = devices;
        this.lastPoll = lastPoll;
        this.updates = updates;

        Text = AppInfo.Name;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(880, 560);
        ClientSize = new Size(940, 640);
        appIcon = LoadAppIcon();
        if (appIcon is not null) Icon = appIcon;
        logo = RenderLogo();
        InitializeAbout();
    }

    /// <summary>A setting or pinned device changed; the owner persists it and re-plans the tray.</summary>
    public event Action? SettingsChanged;

    public event Action? RefreshRequested;

    public event Action? ExitRequested;

    // ---------- showing ----------

    public void ShowWindow()
    {
        Theme = Theme.Current();
        startupEnabled = StartupRegistration.IsEnabled;
        ApplyChrome(tintCaption: true);
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Show();
        Relayout();
        Activate();
    }

    /// <summary>Rebuilds the open window from the current devices and settings.</summary>
    public void RefreshContent()
    {
        if (Visible) Relayout();
    }

    /// <summary>The Windows theme or accent colour changed.</summary>
    public void ReloadTheme()
    {
        Theme = Theme.Current();
        if (!IsHandleCreated) return;
        ApplyChrome(tintCaption: true);
        RefreshContent();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // The X button hides the window; the app is quit from the tray menu or the sidebar.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        RefreshContent();
    }

    // ---------- layout ----------

    protected override void Relayout()
    {
        if (!IsHandleCreated || WindowState == FormWindowState.Minimized || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

        Visuals.Clear();
        int sidebar = S(SidebarWidth), headerHeight = S(100), pad = S(32);
        int x = sidebar + pad;
        int width = Math.Max(S(200), Math.Min(ClientSize.Width - sidebar - pad * 2, S(MaxContentWidth)));
        Viewport = new Rectangle(sidebar, headerHeight, ClientSize.Width - sidebar, Math.Max(0, ClientSize.Height - headerHeight));

        int end = page switch
        {
            Page.Settings => BuildSettings(x, width),
            Page.About => BuildAbout(x, width),
            _ => BuildDevices(x, width),
        };
        ContentHeight = end + S(24);

        AddSidebar(sidebar);
        AddHeader(sidebar, pad);
        FinishLayout();
    }

    void AddHeader(int sidebar, int pad)
    {
        var (title, subtitle) = page switch
        {
            Page.Settings => (Strings.NavSettings, Strings.SettingsSubtitle),
            Page.About => (Strings.NavAbout, Strings.AboutSubtitle),
            _ => (Strings.NavDevices, DevicesSummary()),
        };
        int left = sidebar + pad, right = ClientSize.Width - pad;
        Visuals.Add(new Visual(new Rectangle(left, S(22), right - left, S(44)), false,
            (g, r, _) => DrawText(g, title, HeadingFont, r, Theme.Text)));
        Visuals.Add(new Visual(new Rectangle(left, S(66), right - left, S(22)), false,
            (g, r, _) => DrawText(g, subtitle, BodyFont, r, Theme.Muted)));

        if (page != Page.Devices) return;
        var refresh = new Rectangle(right - S(112), S(28), S(112), S(36));
        Visuals.Add(new Visual(new Rectangle(refresh.X - S(220), refresh.Y, S(208), refresh.Height), false,
            (g, r, _) => DrawText(g, Strings.UpdatedAt(lastPoll()), SmallFont, r, Theme.Muted, TextFormatFlags.Right)));
        AddButton(refresh, "\uE72C", Strings.RefreshShort, () => RefreshRequested?.Invoke());
    }

    string DevicesSummary()
    {
        var list = devices();
        return Strings.DevicesSubtitle(list.Count(d => d.Link != LinkStatus.Disconnected), list.Count);
    }

    void AddSidebar(int sidebar)
    {
        int pad = S(12), itemHeight = S(40);
        Visuals.Add(new Visual(new Rectangle(0, 0, sidebar, ClientSize.Height), false, (g, r, _) =>
        {
            using var pen = new Pen(Theme.Track);
            g.DrawLine(pen, r.Right - 1, 0, r.Right - 1, r.Bottom);
            var tile = new Rectangle(pad + S(6), S(26), S(30), S(30));
            FillRound(g, Theme.Accent, tile, S(8));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(logo, Rectangle.Inflate(tile, -S(3), -S(3)));
            DrawText(g, AppInfo.Name, TitleFont, new Rectangle(pad + S(44), S(24), sidebar - pad * 2 - S(44), S(34)), Theme.Text);
        }));

        var items = new (Page Page, string Glyph, string Label)[]
        {
            (Page.Devices, "\uE772", Strings.NavDevices),
            (Page.Settings, "\uE713", Strings.NavSettings),
            (Page.About, "\uE946", Strings.NavAbout),
        };
        int y = S(84);
        foreach (var item in items)
        {
            var target = item.Page;
            bool selected = target == page;
            Visuals.Add(new Visual(new Rectangle(pad, y, sidebar - pad * 2 - 1, itemHeight), false, (g, r, hot) =>
            {
                if (selected) FillRound(g, Theme.Card, r, S(6));
                else if (hot) FillRound(g, Color.FromArgb(110, Theme.Track), r, S(6));
                if (selected) FillRound(g, Theme.Accent, new Rectangle(r.X + S(2), r.Y + (r.Height - S(16)) / 2, S(3), S(16)), S(2));
                DrawText(g, item.Glyph, IconFont, new Rectangle(r.X + S(14), r.Y, S(24), r.Height), selected ? Theme.Accent : Theme.Text, TextFormatFlags.HorizontalCenter);
                DrawText(g, item.Label, BodyFont, new Rectangle(r.X + S(48), r.Y, r.Width - S(56), r.Height), Theme.Text);
            }, () => Navigate(target)));
            y += itemHeight + S(4);
        }

        Visuals.Add(new Visual(new Rectangle(pad, ClientSize.Height - pad - itemHeight, sidebar - pad * 2 - 1, itemHeight), false, (g, r, hot) =>
        {
            if (hot) FillRound(g, Color.FromArgb(110, Theme.Track), r, S(6));
            DrawText(g, "\uE7E8", IconFont, new Rectangle(r.X + S(14), r.Y, S(24), r.Height), Theme.Muted, TextFormatFlags.HorizontalCenter);
            DrawText(g, Strings.Exit, BodyFont, new Rectangle(r.X + S(48), r.Y, r.Width - S(56), r.Height), Theme.Muted);
        }, () => ExitRequested?.Invoke()));
    }

    void Navigate(Page target)
    {
        if (target == page) return;
        page = target;
        ScrollOffset = 0;
        Relayout();
    }

    // ---------- shared widgets ----------

    void AddButton(Rectangle bounds, string glyph, string text, Action click) =>
        AddButton(bounds, glyph, () => text, click);

    void AddButton(Rectangle bounds, string glyph, Func<string> text, Action click, bool scrolls = false, bool onCard = false) =>
        Visuals.Add(new Visual(bounds, scrolls, (g, r, hot) =>
        {
            FillRound(g, onCard ? (hot ? Theme.Track : Theme.Back) : hot ? Theme.CardHover : Theme.Card, r, S(6));
            DrawText(g, glyph, IconFont, new Rectangle(r.X + S(10), r.Y, S(22), r.Height), Theme.Accent, TextFormatFlags.HorizontalCenter);
            DrawText(g, text(), BodyFont, new Rectangle(r.X + S(38), r.Y, r.Width - S(46), r.Height), Theme.Text);
        }, click));

    void AddSectionLabel(string text, int x, int y, int width) =>
        Visuals.Add(new Visual(new Rectangle(x, y, width, S(28)), true,
            (g, r, _) => DrawText(g, text, NameFont, r, Theme.Muted)));

    void Changed() => SettingsChanged?.Invoke();

    // ---------- icon ----------

    static Icon? LoadAppIcon()
    {
        try { return Environment.ProcessPath is { } path ? Icon.ExtractAssociatedIcon(path) : null; }
        catch (Exception e) when (e is ArgumentException or IOException) { return null; }
    }

    // The tray's hub battery drawn white, to sit on an accent-coloured tile.
    static Bitmap RenderLogo()
    {
        using var owned = TrayIconRenderer.Render(new IconGlyph("", IconTone.Hub), 64, lightTaskbar: false);
        return owned.Icon.ToBitmap();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeAbout();
            logo.Dispose();
            appIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
