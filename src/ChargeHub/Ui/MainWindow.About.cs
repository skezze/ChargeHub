using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ChargeHub.Updates;

namespace ChargeHub.Ui;

internal sealed partial class MainWindow
{
    System.Windows.Forms.Timer? copiedTimer;
    bool copied;

    void InitializeAbout()
    {
        copiedTimer = new System.Windows.Forms.Timer { Interval = 1800 };
        copiedTimer.Tick += (_, _) =>
        {
            copiedTimer.Stop();
            copied = false;
            Invalidate();
        };
    }

    void DisposeAbout() => copiedTimer?.Dispose();

    int BuildAbout(int x, int width)
    {
        int y = 0, gap = S(12), pad = S(24);

        // identity card
        int textX = x + pad + S(64) + S(20), textWidth = width - (textX - x) - pad;
        int descriptionHeight = MeasureHeight(Strings.AboutDescription, BodyFont, textWidth);
        int identityHeight = Math.Max(S(130), S(92) + descriptionHeight + pad);
        Visuals.Add(new Visual(new Rectangle(x, y, width, identityHeight), true, (g, r, _) =>
        {
            FillRound(g, Theme.Card, r, S(8));
            var tile = new Rectangle(r.X + pad, r.Y + pad, S(64), S(64));
            FillRound(g, Theme.Accent, tile, S(14));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(logo, Rectangle.Inflate(tile, -S(8), -S(8)));
            DrawText(g, AppInfo.Name, HeadingFont, new Rectangle(textX, r.Y + S(18), textWidth, S(40)), Theme.Text);
            DrawText(g, Strings.VersionLabel(AppInfo.Version), BodyFont, new Rectangle(textX, r.Y + S(58), textWidth, S(22)), Theme.Muted);
            DrawWrapped(g, Strings.AboutDescription, BodyFont, new Rectangle(textX, r.Y + S(92), textWidth, descriptionHeight + S(4)), Theme.Text);
        }));
        y += identityHeight + gap;

        // updates
        int updateHeight = S(76);
        Visuals.Add(new Visual(new Rectangle(x, y, width, updateHeight), true, (g, r, _) =>
        {
            FillRound(g, Theme.Card, r, S(8));
            bool available = updates.Status == UpdateStatus.Available;
            DrawText(g, Strings.UpdatesTitle, NameFont, new Rectangle(r.X + pad, r.Y + S(14), S(300), S(22)), Theme.Text);
            var status = Strings.UpdateStatusText(updates.Status, updates.Available, settings.LastUpdateCheck, settings.CheckForUpdates);
            DrawText(g, status, BodyFont, new Rectangle(r.X + pad, r.Y + S(38), r.Width - pad * 2 - S(330), S(24)), available ? Theme.Accent : Theme.Muted);
        }));
        int buttonRight = x + width - S(16), checkWidth = ButtonWidth(Strings.CheckNow);
        int buttonTop = y + (updateHeight - S(40)) / 2;
        AddButton(new Rectangle(buttonRight - checkWidth, buttonTop, checkWidth, S(40)), "\uE895", () => Strings.CheckNow,
            () => _ = updates.CheckNowAsync(), scrolls: true, onCard: true);
        if (updates.Available is { } release && updates.Status == UpdateStatus.Available)
        {
            int downloadWidth = ButtonWidth(Strings.DownloadUpdate);
            AddButton(new Rectangle(buttonRight - checkWidth - S(8) - downloadWidth, buttonTop, downloadWidth, S(40)), "\uE896", () => Strings.DownloadUpdate,
                () => OpenUrl(release.PageUrl), scrolls: true, onCard: true);
        }
        y += updateHeight + gap;

        // actions
        int buttonHeight = S(40), buttonX = x;
        var actions = new (string Glyph, Func<string> Text, int Width, Action Click)[]
        {
            ("\uE838", () => Strings.OpenDataFolder, ButtonWidth(Strings.OpenDataFolder), () => Open(Log.Directory)),
            ("\uE8A5", () => Strings.OpenLog, ButtonWidth(Strings.OpenLog), () => Open(Log.FilePath)),
            ("\uE8C8", () => copied ? Strings.Copied : Strings.CopyReport, Math.Max(ButtonWidth(Strings.CopyReport), ButtonWidth(Strings.Copied)), CopyReport),
        };
        foreach (var action in actions)
        {
            AddButton(new Rectangle(buttonX, y, action.Width, buttonHeight), action.Glyph, action.Text, action.Click, scrolls: true);
            buttonX += action.Width + S(8);
        }
        y += buttonHeight + gap;

        // privacy and trademarks
        int noteWidth = width - pad * 2;
        int privacyHeight = MeasureHeight(Strings.PrivacyNote, SmallFont, noteWidth);
        int disclaimerHeight = MeasureHeight(Strings.Disclaimer, SmallFont, noteWidth);
        int notesHeight = pad + privacyHeight + S(14) + disclaimerHeight + pad;
        Visuals.Add(new Visual(new Rectangle(x, y, width, notesHeight), true, (g, r, _) =>
        {
            FillRound(g, Theme.Card, r, S(8));
            DrawWrapped(g, Strings.PrivacyNote, SmallFont, new Rectangle(r.X + pad, r.Y + pad, noteWidth, privacyHeight + S(2)), Theme.Muted);
            DrawWrapped(g, Strings.Disclaimer, SmallFont, new Rectangle(r.X + pad, r.Y + pad + privacyHeight + S(14), noteWidth, disclaimerHeight + S(2)), Theme.Muted);
        }));
        return y + notesHeight;
    }

    int ButtonWidth(string text) => S(46) + MeasureWidth(text, BodyFont) + S(8);

    void CopyReport()
    {
        try
        {
            Clipboard.SetText(DiagnosticReport.Build(devices(), lastPoll(), includeIds: false));
            copied = true;
            copiedTimer?.Stop();
            copiedTimer?.Start();
            Invalidate();
        }
        catch (ExternalException e) { Log.Error("Could not copy the diagnostic report", e); } // the clipboard can be locked by another app
    }

    static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException) { Log.Error("Could not open the release page", e); }
    }

    /// <summary>Opens the release page of a found update (also used by the tray menu).</summary>
    public static void OpenRelease(ReleaseInfo release) => OpenUrl(release.PageUrl);

    static void Open(string path)
    {
        try
        {
            Directory.CreateDirectory(Log.Directory);
            // The log file only exists after the first entry; fall back to its folder.
            if (!File.Exists(path) && !Directory.Exists(path)) path = Log.Directory;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception e) when (e is Win32Exception or IOException or UnauthorizedAccessException)
        {
            Log.Error($"Could not open {path}", e);
        }
    }
}
