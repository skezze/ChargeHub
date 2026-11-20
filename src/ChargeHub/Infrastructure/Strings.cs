using System.Globalization;
using ChargeHub.Core;

namespace ChargeHub;

/// <summary>All user-visible text. Russian for Russian UI cultures, English otherwise.</summary>
internal static class Strings
{
    static bool SystemIsRussian => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";

    static bool russian = SystemIsRussian;

    public static bool Russian => russian;

    /// <summary>Applies the language setting: "auto" follows the Windows UI language, "en" / "ru" force one.</summary>
    public static void Apply(string language) => russian = language switch
    {
        "ru" => true,
        "en" => false,
        _ => SystemIsRussian,
    };

    static string L(string en, string ru) => Russian ? ru : en;

    public static string Refresh => L("Refresh now", "Обновить сейчас");
    public static string NoDevices => L("No Bluetooth or dongle devices found", "Устройства не найдены");
    public static string NotConnectedGroup => L("Not connected", "Не подключены");
    public static string HideDisconnected => L("Hide icons of disconnected devices", "Скрывать значки отключённых устройств");
    public static string LowBatteryNotifications => L("Low battery notifications", "Уведомлять о низком заряде");
    public static string StartWithWindows => L("Start with Windows", "Запускать с Windows");
    public static string Exit => L("Exit", "Выход");
    public static string HubTooltip => L("ChargeHub — click to see your devices", "ChargeHub — нажмите, чтобы увидеть устройства");
    public static string ShowInTray => L("In tray", "В трее");
    public static string LowBatteryTitle => L("Low battery", "Низкий заряд");
    public static string StartupFailed(string reason) => L($"Could not change autostart: {reason}", $"Не удалось изменить автозапуск: {reason}");


    // ---------- main window ----------

    public static string OpenApp => L("Open ChargeHub", "Открыть ChargeHub");
    public static string RefreshShort => L("Refresh", "Обновить");
    public static string NavDevices => L("Devices", "Устройства");
    public static string NavSettings => L("Settings", "Настройки");
    public static string NavAbout => L("About", "О программе");

    public static string DevicesSubtitle(int connected, int total) =>
        total == 0 ? L("Nothing found yet", "Пока ничего не найдено")
                   : L($"{connected} connected · {total} total", $"Подключено: {connected} · всего: {total}");

    public static string UpdatedAt(DateTimeOffset? time) =>
        time is { } at ? L($"Updated {at.LocalDateTime:T}", $"Обновлено {at.LocalDateTime:T}") : L("Updating…", "Обновление…");

    public static string ConnectedGroup => L("Connected", "Подключены");
    public static string NoDevicesTitle => L("No devices yet", "Устройств пока нет");
    public static string NoDevicesHint => L(
        "Pair a Bluetooth device or plug in a 2.4 GHz receiver (ASUS ROG Gladius III Wireless is supported). It will show up here on its own.",
        "Подключите Bluetooth-устройство или вставьте донгл 2.4 ГГц (поддерживается ASUS ROG Gladius III Wireless). Оно появится здесь само.");
    public static string PinHint => L("Click a card to show or hide its tray icon.", "Нажмите на карточку, чтобы показать или скрыть значок в трее.");

    public static string SettingsSubtitle => L("Tray, notifications and polling", "Трей, уведомления и опрос");
    public static string SectionTray => L("Tray", "Трей");
    public static string SectionNotifications => L("Notifications", "Уведомления");
    public static string SectionGeneral => L("General", "Общие");
    public static string HideDisconnectedHint => L(
        "A pinned device's icon disappears while it is off or out of range, and returns when it reconnects.",
        "Значок закреплённого устройства пропадает, пока оно выключено или вне зоны, и возвращается при подключении.");
    public static string LowBatteryNotificationsHint => L(
        "Show a notification once when a pinned device drops to the threshold below.",
        "Показать уведомление один раз, когда закреплённое устройство опустится до порога ниже.");
    public static string LowBatteryThreshold => L("Low battery threshold", "Порог низкого заряда");
    public static string LowBatteryThresholdHint => L(
        "Below this level the tray number turns red.",
        "Ниже этого уровня число в трее становится красным.");
    public static string StartWithWindowsHint => L(
        "Starts quietly in the tray when you sign in.",
        "Запускается тихо в трее при входе в Windows.");
    public static string RefreshInterval => L("Refresh interval", "Интервал опроса");
    public static string RefreshIntervalHint => L(
        "How often batteries are read. Plugging or unplugging a device refreshes immediately.",
        "Как часто читается заряд. При подключении или отключении устройства обновление происходит сразу.");
    public static string IntervalName(int seconds) => seconds switch
    {
        < 60 => L($"{seconds} s", $"{seconds} с"),
        _ => L($"{seconds / 60} min", $"{seconds / 60} мин"),
    };
    public static string Language => L("Language", "Язык");
    public static string LanguageHint => L("Auto follows the Windows display language.", "Авто — язык интерфейса Windows.");
    public static string LanguageName(string code) => code switch
    {
        "en" => "English",
        "ru" => "Русский",
        _ => L("Auto", "Авто"),
    };

    public static string AboutSubtitle => L("Battery levels in your tray", "Заряд устройств в трее");
    public static string AboutDescription => L(
        "ChargeHub shows the battery level of Bluetooth devices and 2.4 GHz dongle mice as small number icons in the notification area.",
        "ChargeHub показывает заряд Bluetooth-устройств и мышей с донглом 2.4 ГГц маленькими значками с числом в области уведомлений.");
    public static string VersionLabel(string version) => L($"Version {version}", $"Версия {version}");
    public static string OpenDataFolder => L("Open data folder", "Папка с данными");
    public static string OpenLog => L("Open log", "Открыть журнал");
    public static string CopyReport => L("Copy diagnostics", "Копировать диагностику");
    public static string Copied => L("Copied", "Скопировано");
    public static string PrivacyNote => L(
        "ChargeHub reads batteries locally and sends no telemetry. Its only network request is a once-a-day check of the public GitHub releases page, which you can turn off in Settings. The diagnostic report contains no device addresses.",
        "ChargeHub читает заряд локально и не отправляет телеметрию. Единственный сетевой запрос — проверка публичных релизов на GitHub раз в сутки; её можно выключить в настройках. Диагностика не содержит адресов устройств.");
    public static string Disclaimer => L(
        "ASUS and ROG are trademarks of ASUSTeK Computer Inc. ChargeHub is an independent project and is not affiliated with or endorsed by ASUS.",
        "ASUS и ROG — торговые марки ASUSTeK Computer Inc. ChargeHub — независимый проект, не связанный с ASUS и не одобренный ею.");


    // ---------- updates ----------

    public static string CheckUpdatesSetting => L("Check for updates daily", "Проверять обновления раз в сутки");
    public static string CheckUpdatesHint => L(
        "Asks GitHub once a day whether a newer release exists. Nothing is downloaded or installed automatically.",
        "Раз в сутки спрашивает GitHub, вышла ли новая версия. Ничего не скачивается и не устанавливается само.");
    public static string UpdatesTitle => L("Updates", "Обновления");
    public static string CheckNow => L("Check for updates", "Проверить обновления");
    public static string DownloadUpdate => L("Download", "Скачать");
    public static string UpdateAvailableTitle => L("Update available", "Доступно обновление");
    public static string UpdateAvailableText(Version version) =>
        L($"ChargeHub {version.ToString(3)} is out. Open About to download it.", $"Вышла версия ChargeHub {version.ToString(3)}. Скачать можно в разделе «О программе».");
    public static string MenuUpdate(Version version) => L($"Update available: {version.ToString(3)}…", $"Доступно обновление: {version.ToString(3)}…");

    public static string UpdateStatusText(Updates.UpdateStatus status, Updates.ReleaseInfo? release, DateTimeOffset? lastCheck, bool automatic) => status switch
    {
        Updates.UpdateStatus.Checking => L("Checking…", "Проверка…"),
        Updates.UpdateStatus.Available when release is not null => L($"Version {release.Version.ToString(3)} is available", $"Доступна версия {release.Version.ToString(3)}"),
        Updates.UpdateStatus.UpToDate => L("You have the latest version", "У вас последняя версия") + CheckedAt(lastCheck),
        Updates.UpdateStatus.Failed => L("Could not reach GitHub. Try again later.", "Не удалось связаться с GitHub. Попробуйте позже."),
        _ when lastCheck is not null => L("You have the latest version", "У вас последняя версия") + CheckedAt(lastCheck),
        _ => automatic ? L("Not checked yet", "Ещё не проверялось") : L("Automatic checks are off", "Автоматическая проверка выключена"),
    };

    static string CheckedAt(DateTimeOffset? time) =>
        time is { } at ? L($" · checked {at.LocalDateTime:g}", $" · проверено {at.LocalDateTime:g}") : "";

    public static string TransportName(Transport transport) => transport switch
    {
        Transport.Bluetooth => "Bluetooth",
        Transport.BluetoothLE => "Bluetooth LE",
        Transport.Usb => "USB",
        Transport.Dongle => "2.4 GHz",
        _ => transport.ToString(),
    };

    /// <summary>One-line status such as "78% · charging" or "asleep · last 7% at 12:30".</summary>
    public static string Status(Device device)
    {
        var parts = new List<string>();
        switch (device.Link)
        {
            case LinkStatus.Connected when device.Percent is int percent:
                parts.Add($"{percent}%");
                if (device.Charging) parts.Add(L("charging", "заряжается"));
                return string.Join(" · ", parts);
            case LinkStatus.Connected:
                if (device.LastPercent is null) parts.Add(L("connected · no battery data", "подключено · нет данных о заряде"));
                break;
            case LinkStatus.Asleep:
                parts.Add(L("asleep", "спит"));
                break;
            case LinkStatus.Unavailable:
                parts.Add(L("battery unavailable", "заряд недоступен"));
                break;
            default:
                parts.Add(L("not connected", "не подключено"));
                break;
        }
        if (device.LastPercent is int last)
        {
            var at = device.LastPercentAt is { } time ? $" {L("at", "в")} {time.LocalDateTime.ToString("t", CultureInfo.CurrentCulture)}" : "";
            parts.Add($"{L("last", "последнее")} {last}%{at}");
        }
        return string.Join(" · ", parts);
    }

    /// <summary>Second line of a device card: transport and state, without repeating the big percentage.</summary>
    public static string CardSubtitle(Device device) =>
        device is { Link: LinkStatus.Connected, Percent: not null }
            ? $"{TransportName(device.Transport)} · {(device.Charging ? L("charging", "заряжается") : L("connected", "подключено"))}"
            : $"{TransportName(device.Transport)} · {Status(device)}";

    public static string MenuLabel(Device device) => $"{device.Name} — {Status(device)}";

    public static string Tooltip(Device device) => $"{device.Name}\n{Status(device)} · {TransportName(device.Transport)}";
}
