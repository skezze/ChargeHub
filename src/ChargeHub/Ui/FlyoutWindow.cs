using ChargeHub.Core;
using ChargeHub.Settings;

namespace ChargeHub.Ui;

/// <summary>
/// The quick panel that opens above the tray on click: one card per device with its level, and the
/// most used settings as switches. The full window (<see cref="MainWindow"/>) is one click away.
/// </summary>
internal sealed class FlyoutWindow : PaintedForm
{
    const int DesignWidth = 360, DesignMaxHeight = 620;

    readonly AppSettings settings;
    readonly Func<IReadOnlyList<Device>> devices;
    bool startupEnabled, anchorRight = true, anchorBottom = true;

    public FlyoutWindow(AppSettings settings, Func<IReadOnlyList<Device>> devices)
    {
        this.settings = settings;
        this.devices = devices;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        KeyPreview = true;
        Text = AppInfo.Name;
        _ = Handle;
    }

    public event Action? SettingsChanged;

    public event Action? RefreshRequested;

    public event Action? OpenRequested;

    public event Action? ExitRequested;

    /// <summary>When the panel last closed; lets the owner ignore the tray click that merely dismissed it.</summary>
    public DateTime LastHiddenUtc { get; private set; } = DateTime.MinValue;

    // ---------- showing / placing ----------

    public void ShowNearTray()
    {
        Theme = Theme.Current();
        startupEnabled = StartupRegistration.IsEnabled;
        ScrollOffset = 0;

        var cursor = Cursor.Position;
        var area = Screen.FromPoint(cursor).WorkingArea;
        anchorRight = cursor.X >= area.Left + area.Width / 2;
        anchorBottom = cursor.Y >= area.Top + area.Height / 2;

        Relayout();
        ApplyChrome(tintCaption: false);
        Show();
        Activate();
    }

    /// <summary>Rebuilds the panel from the current devices and settings if it is open.</summary>
    public void RefreshContent()
    {
        if (Visible) Relayout();
    }

    protected override void Relayout()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        BuildLayout(area);
        int margin = S(12);
        int x = anchorRight ? area.Right - Width - margin : area.Left + margin;
        int y = anchorBottom ? area.Bottom - Height - margin : area.Top + margin;
        SetBounds(x, y, Width, Height);
        FinishLayout();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            if (Environment.OSVersion.Version.Build < 22000) cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
            return cp;
        }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Hide();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) LastHiddenUtc = DateTime.UtcNow;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Hide(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ---------- layout ----------

    void BuildLayout(Rectangle workingArea)
    {
        Visuals.Clear();
        int width = S(DesignWidth), pad = S(12), gap = S(8);
        int cardWidth = width - pad * 2;
        int headerHeight = S(52), rowHeight = S(40), buttonHeight = S(36);
        int footerCardHeight = rowHeight * 3;
        int footerHeight = gap + footerCardHeight + gap + buttonHeight + pad;

        // header
        Visuals.Add(new Visual(new Rectangle(pad + S(4), S(10), cardWidth - S(48), S(32)), false,
            (g, r, _) => DrawText(g, AppInfo.Name, TitleFont, r, Theme.Text)));
        Visuals.Add(new Visual(new Rectangle(width - pad - S(36), S(10), S(36), S(32)), false,
            (g, r, hot) =>
            {
                if (hot) FillRound(g, Theme.Card, r, S(6));
                DrawText(g, "\uE72C", IconFont, r, Theme.Text, TextFormatFlags.HorizontalCenter);
            }, () => RefreshRequested?.Invoke()));

        // devices (scrollable)
        var list = devices();
        int y = 0;
        if (list.Count == 0)
        {
            Visuals.Add(new Visual(new Rectangle(pad, 0, cardWidth, S(56)), true,
                (g, r, _) =>
                {
                    FillRound(g, Theme.Card, r, S(8));
                    DrawText(g, Strings.NoDevices, BodyFont, Rectangle.Inflate(r, -S(16), 0), Theme.Muted);
                }));
            y = S(56);
        }
        foreach (var device in list)
        {
            bool compact = device.Link == LinkStatus.Disconnected;
            int height = compact ? S(56) : S(84);
            var captured = device;
            Visuals.Add(new Visual(new Rectangle(pad, y, cardWidth, height), true,
                (g, r, hot) => PaintDeviceCard(g, r, captured, settings.Pinned.Contains(captured.Id), hot, compact, large: false, settings.LowBatteryPercent),
                () => TogglePinned(captured.Id)));
            y += height + gap;
        }
        ContentHeight = Math.Max(0, y - gap);

        int maxHeight = Math.Min(S(DesignMaxHeight), workingArea.Height - S(24));
        int viewportHeight = Math.Clamp(ContentHeight, S(56), Math.Max(S(56), maxHeight - headerHeight - footerHeight));
        Viewport = new Rectangle(0, headerHeight, width, viewportHeight);

        // footer: settings card + buttons
        int footerTop = headerHeight + viewportHeight + gap;
        var card = new Rectangle(pad, footerTop, cardWidth, footerCardHeight);
        Visuals.Add(new Visual(card, false, (g, r, _) => FillRound(g, Theme.Card, r, S(8))));
        AddToggleRow(card.X, card.Y, cardWidth, rowHeight, Strings.HideDisconnected, () => settings.HideDisconnected,
            () => { settings.HideDisconnected = !settings.HideDisconnected; SettingsChanged?.Invoke(); });
        AddToggleRow(card.X, card.Y + rowHeight, cardWidth, rowHeight, Strings.LowBatteryNotifications, () => settings.LowBatteryNotifications,
            () => { settings.LowBatteryNotifications = !settings.LowBatteryNotifications; SettingsChanged?.Invoke(); });
        AddToggleRow(card.X, card.Y + rowHeight * 2, cardWidth, rowHeight, Strings.StartWithWindows, () => startupEnabled, ToggleStartup);

        int half = (cardWidth - gap) / 2;
        AddButton(new Rectangle(pad, card.Bottom + gap, half, buttonHeight), Strings.OpenApp, Theme.Accent, () => OpenRequested?.Invoke());
        AddButton(new Rectangle(pad + half + gap, card.Bottom + gap, cardWidth - half - gap, buttonHeight), Strings.Exit, Theme.Muted, () => ExitRequested?.Invoke());

        ClientSize = new Size(width, headerHeight + viewportHeight + footerHeight);
    }

    void AddButton(Rectangle bounds, string text, Color color, Action click) =>
        Visuals.Add(new Visual(bounds, false, (g, r, hot) =>
        {
            FillRound(g, hot ? Theme.CardHover : Theme.Card, r, S(6));
            DrawText(g, text, BodyFont, r, color, TextFormatFlags.HorizontalCenter);
        }, click));

    void AddToggleRow(int x, int y, int width, int height, string label, Func<bool> isOn, Action toggle)
    {
        Visuals.Add(new Visual(new Rectangle(x, y, width, height), false,
            (g, r, hot) =>
            {
                if (hot) FillRound(g, Theme.CardHover, Rectangle.Inflate(r, -S(4), -S(2)), S(6));
                var text = new Rectangle(r.X + S(16), r.Y, r.Width - S(16) - S(16) - S(48), r.Height);
                DrawText(g, label, BodyFont, text, Theme.Text);
                PaintSwitch(g, new Rectangle(r.Right - S(16) - S(40), r.Y + (r.Height - S(20)) / 2, S(40), S(20)), isOn());
            }, toggle));
    }

    void TogglePinned(string id)
    {
        if (!settings.Pinned.Remove(id)) settings.Pinned.Add(id);
        SettingsChanged?.Invoke();
    }

    void ToggleStartup()
    {
        if (StartupRegistration.TrySet(!startupEnabled, out var error)) startupEnabled = !startupEnabled;
        else MessageBox.Show(Strings.StartupFailed(error!), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        Relayout();
    }
}
