namespace ChargeHub.Settings;

internal sealed class AppSettings
{
    public const int DefaultRefreshSeconds = 60;
    public static readonly int[] RefreshChoices = [30, 60, 120, 300];
    public static readonly string[] Languages = ["auto", "en", "ru"];

    /// <summary>Ids of devices the user wants to see in the tray.</summary>
    public HashSet<string> Pinned { get; set; } = [];

    /// <summary>Hide the tray icon of a pinned device while it is not connected.</summary>
    public bool HideDisconnected { get; set; } = true;

    public bool LowBatteryNotifications { get; set; } = true;

    public int LowBatteryPercent { get; set; } = 20;

    /// <summary>How often the batteries are polled, in seconds (one of <see cref="RefreshChoices"/>).</summary>
    public int RefreshIntervalSeconds { get; set; } = DefaultRefreshSeconds;

    /// <summary>"auto" (follow Windows), "en" or "ru".</summary>
    public string Language { get; set; } = "auto";

    /// <summary>Look for a newer GitHub release once a day. This is the app's only network traffic.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>When the last successful update check finished.</summary>
    public DateTimeOffset? LastUpdateCheck { get; set; }

    /// <summary>Tag of the newest release the user has already been told about, so the notification appears once.</summary>
    public string? NotifiedUpdateTag { get; set; }
}
