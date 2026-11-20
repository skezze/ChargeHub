using ChargeHub.Settings;

namespace ChargeHub.Ui;

internal sealed partial class MainWindow
{
    /// <summary>One line of a settings card: title, description and a control on the right.</summary>
    sealed record RowSpec(string Title, string Hint, int ControlWidth, Func<bool>? IsOn = null, Action? Click = null, Action<Rectangle>? AddControl = null);

    int BuildSettings(int x, int width)
    {
        int y = 0;

        AddSectionLabel(Strings.SectionTray, x, y, width);
        y = AddCard(x, y + S(32), width,
            Switch(Strings.HideDisconnected, Strings.HideDisconnectedHint, () => settings.HideDisconnected,
                () => settings.HideDisconnected = !settings.HideDisconnected)) + S(20);

        AddSectionLabel(Strings.SectionNotifications, x, y, width);
        y = AddCard(x, y + S(32), width,
            Switch(Strings.LowBatteryNotifications, Strings.LowBatteryNotificationsHint, () => settings.LowBatteryNotifications,
                () => settings.LowBatteryNotifications = !settings.LowBatteryNotifications),
            new RowSpec(Strings.LowBatteryThreshold, Strings.LowBatteryThresholdHint, S(124),
                AddControl: bounds => AddStepper(bounds, () => settings.LowBatteryPercent, 5, 50, 5, value => settings.LowBatteryPercent = value))) + S(20);

        AddSectionLabel(Strings.SectionGeneral, x, y, width);
        int[] intervals = AppSettings.RefreshChoices;
        string[] languages = AppSettings.Languages;
        y = AddCard(x, y + S(32), width,
            new RowSpec(Strings.StartWithWindows, Strings.StartWithWindowsHint, S(40), IsOn: () => startupEnabled, Click: ToggleStartup),
            Switch(Strings.CheckUpdatesSetting, Strings.CheckUpdatesHint, () => settings.CheckForUpdates,
                () => settings.CheckForUpdates = !settings.CheckForUpdates),
            new RowSpec(Strings.RefreshInterval, Strings.RefreshIntervalHint, S(64) * intervals.Length,
                AddControl: bounds => AddSegmented(bounds, intervals.Select(Strings.IntervalName).ToArray(),
                    () => Array.IndexOf(intervals, settings.RefreshIntervalSeconds), index => settings.RefreshIntervalSeconds = intervals[index])),
            new RowSpec(Strings.Language, Strings.LanguageHint, S(84) * languages.Length,
                AddControl: bounds => AddSegmented(bounds, languages.Select(Strings.LanguageName).ToArray(),
                    () => Array.IndexOf(languages, settings.Language), index => settings.Language = languages[index])));
        return y;
    }

    RowSpec Switch(string title, string hint, Func<bool> get, Action flip) =>
        new(title, hint, S(40), IsOn: get, Click: () => { flip(); Changed(); });

    /// <summary>Adds a rounded card holding the rows and returns the y below it.</summary>
    int AddCard(int x, int y, int width, params RowSpec[] rows)
    {
        int padding = S(16);
        int textWidth = width - padding * 2 - rows.Max(r => r.ControlWidth) - S(24);
        var heights = rows.Select(r => Math.Max(S(64), S(36) + (r.Hint.Length == 0 ? 0 : MeasureHeight(r.Hint, SmallFont, textWidth)) + S(14))).ToArray();
        int total = heights.Sum();

        Visuals.Add(new Visual(new Rectangle(x, y, width, total), true, (g, r, _) => FillRound(g, Theme.Card, r, S(8))));

        int top = y;
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var bounds = new Rectangle(x, top, width, heights[i]);
            bool first = i == 0;
            Visuals.Add(new Visual(bounds, true, (g, r, hot) =>
            {
                if (hot) FillRound(g, Theme.CardHover, Rectangle.Inflate(r, -S(4), -S(2)), S(6));
                if (!first)
                {
                    using var pen = new Pen(Theme.Track);
                    g.DrawLine(pen, r.X + padding, r.Y, r.Right - padding, r.Y);
                }
                DrawText(g, row.Title, NameFont, new Rectangle(r.X + padding, r.Y + S(12), textWidth, S(22)), Theme.Text);
                if (row.Hint.Length > 0)
                    DrawWrapped(g, row.Hint, SmallFont, new Rectangle(r.X + padding, r.Y + S(36), textWidth, r.Height - S(36)), Theme.Muted);
                if (row.IsOn is { } isOn)
                    PaintSwitch(g, new Rectangle(r.Right - padding - S(40), r.Y + (r.Height - S(20)) / 2, S(40), S(20)), isOn());
            }, row.Click));

            if (row.AddControl is { } addControl)
                addControl(new Rectangle(bounds.Right - padding - row.ControlWidth, bounds.Y + (bounds.Height - S(32)) / 2, row.ControlWidth, S(32)));
            top += heights[i];
        }
        return y + total;
    }

    void AddSegmented(Rectangle bounds, string[] labels, Func<int> selected, Action<int> choose)
    {
        Visuals.Add(new Visual(bounds, true, (g, r, _) => FillRound(g, Theme.Back, r, S(6))));
        int segment = bounds.Width / labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            var area = new Rectangle(bounds.X + i * segment, bounds.Y, segment, bounds.Height);
            Visuals.Add(new Visual(area, true, (g, r, hot) =>
            {
                bool on = selected() == index;
                var inner = Rectangle.Inflate(r, -S(2), -S(2));
                if (on) FillRound(g, Theme.Accent, inner, S(5));
                else if (hot) FillRound(g, Theme.Track, inner, S(5));
                var textColor = on ? (Theme.IsLight ? Color.White : Color.FromArgb(24, 24, 24)) : Theme.Text;
                DrawText(g, labels[index], BodyFont, r, textColor, TextFormatFlags.HorizontalCenter);
            }, () =>
            {
                if (selected() == index) return;
                choose(index);
                Changed();
            }));
        }
    }

    void AddStepper(Rectangle bounds, Func<int> get, int min, int max, int step, Action<int> set)
    {
        int button = bounds.Height;
        void Button(Rectangle area, string glyph, int delta) =>
            Visuals.Add(new Visual(area, true, (g, r, hot) =>
            {
                FillRound(g, hot ? Theme.Track : Theme.Back, r, S(6));
                DrawText(g, glyph, IconFont, r, Theme.Text, TextFormatFlags.HorizontalCenter);
            }, () =>
            {
                int next = Math.Clamp(get() + delta * step, min, max);
                if (next == get()) return;
                set(next);
                Changed();
            }));

        Button(new Rectangle(bounds.X, bounds.Y, button, button), "\uE738", -1);
        Visuals.Add(new Visual(new Rectangle(bounds.X + button, bounds.Y, bounds.Width - button * 2, bounds.Height), true,
            (g, r, _) => DrawText(g, $"{get()}%", NameFont, r, Theme.Text, TextFormatFlags.HorizontalCenter)));
        Button(new Rectangle(bounds.Right - button, bounds.Y, button, button), "\uE710", 1);
    }

    void ToggleStartup()
    {
        if (StartupRegistration.TrySet(!startupEnabled, out var error)) startupEnabled = !startupEnabled;
        else MessageBox.Show(this, Strings.StartupFailed(error!), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        Relayout();
    }
}
