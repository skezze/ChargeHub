namespace ChargeHub.Tray;

/// <summary>
/// Owns every <see cref="NotifyIcon"/> of the app. Callers describe the icons they want
/// with <see cref="Sync"/>; the host adds, updates and removes real tray icons so that
/// reality always matches the plan, and releases every native resource it created.
/// </summary>
internal sealed class TrayIconHost : IDisposable
{
    sealed class Entry(NotifyIcon notify, OwnedIcon image, TrayIconSpec spec) : IDisposable
    {
        public NotifyIcon Notify { get; } = notify;
        public OwnedIcon Image { get; set; } = image;
        public TrayIconSpec Spec { get; set; } = spec;

        public void Dispose()
        {
            Notify.Visible = false; // removes the icon from the tray before the window behind it goes away
            Notify.Dispose();
            Image.Dispose();
        }
    }

    readonly Dictionary<string, Entry> entries = [];
    readonly ContextMenuStrip menu;
    bool lightTaskbar = TrayIconRenderer.TaskbarIsLight();
    bool disposed;

    public TrayIconHost(ContextMenuStrip menu) => this.menu = menu;

    /// <summary>Raised when any tray icon is left-clicked. Right click opens the context menu on its own.</summary>
    public event Action? LeftClicked;

    public event Action? DoubleClicked;

    public void Sync(IReadOnlyList<TrayIconSpec> specs)
    {
        if (disposed) return;

        // Add and update first, remove last: when the hub icon is replaced by device icons the tray never goes empty.
        foreach (var spec in specs)
        {
            try
            {
                if (entries.TryGetValue(spec.Key, out var entry)) Update(entry, spec);
                else Add(spec);
            }
            catch (Exception e) { Log.Error($"Tray icon '{spec.Key}' could not be updated", e); }
        }

        var wanted = specs.Select(s => s.Key).ToHashSet();
        foreach (var key in entries.Keys.Where(k => !wanted.Contains(k)).ToArray())
        {
            entries[key].Dispose();
            entries.Remove(key);
        }
    }

    /// <summary>Re-renders all icons, e.g. after the taskbar theme or display settings changed.</summary>
    public void RefreshAppearance()
    {
        if (disposed) return;
        lightTaskbar = TrayIconRenderer.TaskbarIsLight();
        foreach (var entry in entries.Values)
            ReplaceImage(entry, entry.Spec.Glyph);
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Warning)
    {
        var target = entries.Values.FirstOrDefault();
        target?.Notify.ShowBalloonTip(8000, title, text, icon);
    }

    void Add(TrayIconSpec spec)
    {
        var image = Render(spec.Glyph);
        NotifyIcon? notify = null;
        try
        {
            notify = new NotifyIcon { Icon = image.Icon, Text = Truncate(spec.Tooltip), ContextMenuStrip = menu };
            notify.MouseUp += OnMouseUp;
            notify.MouseDoubleClick += OnMouseDoubleClick;
            notify.Visible = true;
            entries[spec.Key] = new Entry(notify, image, spec);
        }
        catch
        {
            notify?.Dispose();
            image.Dispose();
            throw;
        }
    }

    void Update(Entry entry, TrayIconSpec spec)
    {
        if (entry.Spec.Glyph != spec.Glyph) ReplaceImage(entry, spec.Glyph);
        if (entry.Spec.Tooltip != spec.Tooltip) entry.Notify.Text = Truncate(spec.Tooltip);
        entry.Spec = spec;
    }

    void ReplaceImage(Entry entry, IconGlyph glyph)
    {
        var fresh = Render(glyph);
        var old = entry.Image;
        entry.Notify.Icon = fresh.Icon;
        entry.Image = fresh;
        old.Dispose();
    }

    OwnedIcon Render(IconGlyph glyph) =>
        TrayIconRenderer.Render(glyph, Math.Max(16, SystemInformation.SmallIconSize.Width), lightTaskbar);

    void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) LeftClicked?.Invoke();
    }

    void OnMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) DoubleClicked?.Invoke();
    }

    // The shell limits tooltips to 127 characters.
    static string Truncate(string text) => text.Length <= 127 ? text : text[..126] + "…";

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var entry in entries.Values) entry.Dispose();
        entries.Clear();
    }
}
