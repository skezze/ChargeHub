using ChargeHub.Tray;
using Microsoft.Win32;

namespace ChargeHub.Ui;

/// <summary>Colours of the flyout, following the Windows light/dark setting and the user's accent colour.</summary>
internal sealed record Theme(
    Color Back, Color Card, Color CardHover, Color Text, Color Muted, Color Track,
    Color Accent, Color Low, Color Charging)
{
    public bool IsLight => Back.R > 128;

    public static Theme Current() => Create(TrayIconRenderer.TaskbarIsLight(), ReadAccent());

    public static Theme Create(bool light, Color accent) => light
        ? new Theme(
            Back: Color.FromArgb(243, 243, 243), Card: Color.FromArgb(255, 255, 255), CardHover: Color.FromArgb(248, 248, 248),
            Text: Color.FromArgb(26, 26, 26), Muted: Color.FromArgb(96, 96, 96), Track: Color.FromArgb(222, 222, 222),
            Accent: accent, Low: Color.FromArgb(196, 43, 28), Charging: Color.FromArgb(16, 137, 62))
        : new Theme(
            Back: Color.FromArgb(32, 32, 32), Card: Color.FromArgb(44, 44, 44), CardHover: Color.FromArgb(53, 53, 53),
            Text: Color.White, Muted: Color.FromArgb(166, 166, 166), Track: Color.FromArgb(70, 70, 70),
            Accent: ControlPaint.Light(accent, 0.25f), Low: Color.FromArgb(255, 99, 90), Charging: Color.FromArgb(76, 214, 126));

    static Color ReadAccent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
                return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException) { }
        return Color.FromArgb(0, 120, 212);
    }
}
