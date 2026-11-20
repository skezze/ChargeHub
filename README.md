# ChargeHub

A tiny Windows tray utility that shows the battery level of your connected devices right next to the clock.

- **Bluetooth** devices: headphones, gamepads, keyboards, mice (Classic and LE).
- **2.4 GHz dongle / USB** devices: ASUS ROG Gladius III Wireless AimPoint (original receiver `0B05:1A72`, cable `0B05:1A70`).
- No telemetry, no cloud, no third-party runtime dependencies. C# / .NET 10, WinForms `NotifyIcon` and native Windows APIs only. The only network request is the once-a-day update check (see [Updates](#updates)).

## Use

Run `ChargeHub.exe`. The main window opens and a small battery icon appears in the tray. Closing the window (X) keeps the app running in the tray; quit it with **Exit** in the sidebar or the tray menu. Starting the app a second time just brings the running window to the front.

- **Main window** (double-click the tray icon, or *Open ChargeHub* in the panel or menu), light/dark theme and accent colour follow Windows:
  - **Devices**: cards for connected and not connected devices with level, progress bar and an **In tray** switch (click a card to toggle it).
  - **Settings**: hide icons of disconnected devices, low battery notifications and threshold, start with Windows, daily update check, refresh interval (30 s – 5 min), language (Auto / English / Русский).
  - **About**: version, update status with *Check for updates* / *Download*, open the data folder or log, copy a diagnostic report (no device addresses), privacy note.
- **Left-click** the icon: a Windows 11-style panel opens above the tray (light/dark theme and accent colour follow Windows). Every device is a card with its level, a progress bar and an **In tray** switch. Switch it on to get a dedicated tray icon with the percentage; switch it off and that icon is removed immediately. The panel also holds the settings and **Exit**. It closes when you click elsewhere or press `Esc`.
- **Right-click** the icon: the same actions as a classic context menu.

When nothing is switched on, the battery icon stays so the panel is always reachable.

| Icon | Meaning |
| --- | --- |
| white / dark number | battery level |
| red number | at or below the low-battery threshold (20 %) |
| green number | charging |
| grey number | no fresh reading: device asleep or unreachable, showing the **last known** value |
| grey `–` | connected, but the device does not report a battery level |

Low battery notifications fire once per discharge cycle, only for devices shown in the tray. With *Auto* the UI is Russian or English depending on the Windows display language. Autostart launches `ChargeHub.exe --background`, which starts in the tray without opening the window.

Windows may place new tray icons under the `^` overflow arrow; drag them out or enable them in *Settings → Personalization → Taskbar → Other system tray icons*.

Requirements: Windows 10 2004+ / Windows 11, x64. The installer checks for the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) and, if it is missing, downloads and installs it from Microsoft (needs administrator rights and an internet connection). The portable zip does not install it, so the runtime must already be present.

## Updates

Once a day (and shortly after start, if the last check is older than 24 h) ChargeHub asks `api.github.com/repos/<owner>/<repo>/releases/latest` whether a newer release exists. If so, you get one notification, an *Update available* item in the tray menu and a *Download* button in *About* that opens the release page. Nothing is downloaded or installed automatically, and no data is sent besides the standard `User-Agent` header. Turn it off in *Settings*; *About → Check for updates* works either way. Drafts and pre-releases are ignored.

## How states are handled

Readings are never invented. Every device is in exactly one state:

| State | Meaning | Percentage |
| --- | --- | --- |
| Connected | device answered | fresh value, or none if it does not report one |
| Asleep | receiver/radio is present but the device is silent (e.g. mouse went to sleep) | last known value, marked stale |
| Unavailable | state could not be determined (access denied, driver error) | last known value, marked stale |
| Disconnected | not connected / receiver unplugged | last known value, marked stale |

A receiver alone is never treated as proof that the mouse is connected.

## Architecture

```
src/ChargeHub
├── Core/        device model, DeviceBook (merge + last-known), BatteryMonitor (polling loop), LowBatteryNotifier
├── Sources/     IBatterySource implementations: BluetoothSource, AsusGladiusSource (+ HID I/O, protocol)
├── Settings/    AppSettings, SettingsStore (JSON), StartupRegistration (HKCU Run)
├── Tray/        TrayPlanner (pure: devices+settings → icons), TrayIconHost (NotifyIcon lifecycle), renderer, context menu
├── Ui/          PaintedForm (custom painting, hit-testing, scrolling), FlyoutWindow (tray panel), MainWindow (Devices / Settings / About), Theme
├── Updates/     UpdateChecker (GitHub releases API), UpdateService (daily schedule, notify once)
├── Infrastructure/  logging, localized strings, hidden message window
└── App/         Program.cs (single-instance entry point, --probe), TrayApp.cs (wiring: sources → monitor → UI thread → planner → tray host), app.manifest
tests/ChargeHub.Tests   xUnit tests for everything that does not need hardware
```

The tray is driven by one rule: `TrayPlanner.Plan(devices, settings)` returns the exact list of icons that should exist, and `TrayIconHost.Sync` makes reality match it, adding, updating (only when the glyph or tooltip actually changed) and disposing `NotifyIcon`s and their GDI handles. Hiding or showing an icon is just a different plan, so there is no separate code path that can leave a phantom icon behind. (If the process is killed from Task Manager, Windows itself keeps the dead icon until you hover over it.)

Polling: one background loop, one poll at a time. It runs at the configured interval (1 min by default) and on demand (Bluetooth connect/disconnect events, USB `WM_DEVICECHANGE`, resume from sleep), with bursts of events collapsed into one poll. A failing source cannot affect the others and is logged once.

### Adding a device

Implement `IBatterySource` (return a `DeviceReading` per device using the `Connected` / `Asleep` / `Disconnected` / `Unavailable` factories), then add it to the list in `TrayApp`. If the same physical device is also reachable through another source, give both the same id (see `KnownDevices`) and they are merged into one.

## Device notes

- **Bluetooth LE**: standard Battery Service (`0x180F` / `0x2A19`) read while connected. Devices without the service are probed once, not every minute.
- **Bluetooth Classic and others**: the battery property Windows' driver publishes for the device (`DEVPKEY_Bluetooth_Battery`), only while connected. Not every device exposes one; then you get `–`.
- **ASUS Gladius III Wireless AimPoint**: only the read-only battery query (`00 12 07`, 65-byte HID reports, interface `MI_00`) is sent. A 1.5 s deadline per query; no DPI, RGB, firmware or profile settings are touched. In Bluetooth mode the mouse is found by the Bluetooth source under the same id. OMNI/Booster receivers and other ASUS models need their own implementation.

## Build

```powershell
./scripts/build.ps1             # run tests, publish framework-dependent build to artifacts/win-x64
./scripts/build.ps1 -Installer  # the same, plus artifacts/installer/ChargeHub-<version>-setup.exe (needs Inno Setup 6)
```

GitHub Actions builds every push to `main` and pull request (`build.yml`). Pushing a version tag publishes a release (`release.yml`):

```powershell
# 1. bump <Version> in src/ChargeHub/ChargeHub.csproj and commit
git tag vX.Y.Z
git push origin vX.Y.Z
```

The workflow checks that the tag matches `<Version>`, runs the tests and attaches two files to the release: `ChargeHub-<version>-setup.exe` (installer, about 6 MB, installs the .NET 10 Desktop Runtime when it is missing) and a portable zip that needs the runtime already installed. Release notes are generated from the commits. The repository the app checks is `AppInfo.Repository`.

The installer is `installer/ChargeHub.iss` (Inno Setup 6). It is not signed, so Windows SmartScreen may warn on first run.

### Diagnostics

`ChargeHub.exe --probe` reads all sources once and prints the result (including device ids) to the console. Attach its output to bug reports; the same report without ids is available in *About → Copy diagnostics*.

Preferences: `%LOCALAPPDATA%\ChargeHub\settings.json` (contains local Bluetooth identifiers; do not commit it). Log: `chargehub.log` in the same directory (rotates at ~512 KB). Autostart: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\ChargeHub`.

## Verification status

Unit tests cover the ASUS protocol, merging/stale-value rules, tray planning, low-battery logic and the polling loop. On the development PC `--probe` correctly saw the ASUS receiver (reported asleep while the mouse was idle, from a genuine `FF AA` receiver response) and a paired but disconnected Bluetooth headset. Charging state over Bluetooth is not available in a standard way, so it is shown only for the ASUS protocol. Hardware behaviour with other firmware, drivers and devices has not been validated.

ASUS and ROG are trademarks of their respective owners. ChargeHub is not affiliated with ASUS.
