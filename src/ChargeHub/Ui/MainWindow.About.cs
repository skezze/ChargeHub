using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ChargeHub.Updates;

namespace ChargeHub.Ui;

internal sealed partial class MainWindow
{
    System.Windows.Forms.Timer? copiedTimer;
    bool copied;
    bool installingUpdate;
    readonly CancellationTokenSource updateCancellation = new();

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

    void DisposeAbout()
    {
        copiedTimer?.Dispose();
        updateCancellation.Cancel();
        updateCancellation.Dispose();
    }

    public void ShowAbout()
    {
        page = Page.About;
        ShowWindow();
        RefreshContent();
    }

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
        string? actionLabel = null;
        InstallerAsset? asset = null;
        var release = updates.Status == UpdateStatus.Available ? updates.Available : null;
        if (!installingUpdate && release is not null)
        {
            asset = release.Installer is not null && InstallerUpdate.IsInstalledCopy() ? release.Installer : null;
            actionLabel = asset is null ? Strings.DownloadUpdate : Strings.InstallUpdate;
        }
        int checkWidth = ButtonWidth(Strings.CheckNow);
        int actionWidth = actionLabel is null ? 0 : ButtonWidth(actionLabel) + S(8);
        int buttonsWidth = installingUpdate ? 0 : checkWidth + actionWidth;
        Visuals.Add(new Visual(new Rectangle(x, y, width, updateHeight), true, (g, r, _) =>
        {
            FillRound(g, Theme.Card, r, S(8));
            bool available = updates.Status == UpdateStatus.Available;
            DrawText(g, Strings.UpdatesTitle, NameFont, new Rectangle(r.X + pad, r.Y + S(14), S(300), S(22)), Theme.Text);
            var status = installingUpdate ? Strings.InstallingUpdate :
                Strings.UpdateStatusText(updates.Status, updates.Available, settings.LastUpdateCheck, settings.CheckForUpdates);
            DrawText(g, status, BodyFont, new Rectangle(r.X + pad, r.Y + S(38), Math.Max(0, r.Width - pad * 2 - buttonsWidth), S(24)), available ? Theme.Accent : Theme.Muted);
        }));
        int buttonRight = x + width - S(16);
        int buttonTop = y + (updateHeight - S(40)) / 2;
        if (!installingUpdate)
        {
            AddButton(new Rectangle(buttonRight - checkWidth, buttonTop, checkWidth, S(40)), "\uE895", () => Strings.CheckNow,
                () => _ = updates.CheckNowAsync(), scrolls: true, onCard: true);
            if (release is not null && actionLabel is not null)
                AddButton(new Rectangle(buttonRight - buttonsWidth, buttonTop, actionWidth - S(8), S(40)), "\uE896", () => actionLabel,
                    () => { if (asset is null) OpenUrl(release.PageUrl); else _ = InstallUpdateAsync(asset); }, scrolls: true, onCard: true);
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

    async Task InstallUpdateAsync(InstallerAsset asset)
    {
        if (installingUpdate) return;
        var cancellation = updateCancellation.Token;
        installingUpdate = true;
        RefreshContent();
        try
        {
            await InstallerUpdate.DownloadAndLaunchAsync(asset, cancellation);
            ExitRequested?.Invoke();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or HttpRequestException or
            OperationCanceledException or Win32Exception or InvalidOperationException)
        {
            if (cancellation.IsCancellationRequested || e is Win32Exception { NativeErrorCode: 1223 }) return;
            Log.Error("Installing the update failed", e);
            MessageBox.Show(this, Strings.InstallUpdateFailed, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            installingUpdate = false;
            if (!IsDisposed) RefreshContent();
        }
    }

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
