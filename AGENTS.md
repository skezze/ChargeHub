# ChargeHub: notes for AI agents

Read this before exploring the repo; it is meant to save you from scanning files. User docs and design rationale are in `README.md`.

## What it is

Windows 10 2004+ / 11 (x64) tray app, version in `src/ChargeHub/ChargeHub.csproj` (`<Version>`). Shows battery levels of Bluetooth devices and the ASUS ROG Gladius III Wireless AimPoint (USB/2.4 GHz dongle or wired). C# / .NET 10, WinForms `NotifyIcon` and native Windows APIs. No third-party NuGet packages in the app. Only network call: once-a-day GitHub releases check.

## Map

- `src/ChargeHub/`
  - `App/Program.cs`: entry point. Single-instance mutex `Local\ChargeHub.Tray`. Flags: `--background` (autostart, no window), `--probe` (console dump, see `Probe.cs`).
  - `App/TrayApp.cs`: wiring. Sources -> `BatteryMonitor` -> UI thread -> `TrayPlanner` -> `TrayIconHost`.
  - `Core/` (no UI, mostly pure): `Device.cs` (`DeviceReading`, `Device`, `LinkStatus`, `Transport`), `DeviceBook.cs` (merge by id, last-known values), `BatteryMonitor.cs` (poll loop, debounce, source timeouts), `LowBatteryNotifier.cs`, `IBatterySource.cs`, `KnownDevices.cs` (stable ids and names).
  - `Sources/`: `BluetoothSource.cs` (BLE Battery Service), `BluetoothPnpBattery.cs` (`DEVPKEY_Bluetooth_Battery` via SetupAPI), `AsusGladiusSource.cs` + `AsusProtocol.cs` (packet encode/decode, no I/O), `HidInterface.cs` (HID device I/O).
  - `Tray/`: `TrayPlanner.cs` (pure: devices + settings -> wanted icons), `TrayIconHost.cs` (`NotifyIcon` lifecycle, `Sync`), `TrayIconRenderer.cs` (glyph bitmaps), `TrayMenu.cs` (classic right-click menu).
  - `Ui/`: `FlyoutWindow.cs` (Windows 11-style panel), `MainWindow.cs` + partials `MainWindow.Devices.cs` / `.Settings.cs` / `.About.cs`, `PaintedForm.cs` (custom-painted base with scrolling and hit-testing), `Theme.cs` (light/dark/accent).
  - `Settings/`: `AppSettings.cs` (model), `SettingsStore.cs` (JSON in `%LOCALAPPDATA%\ChargeHub\settings.json`), `StartupRegistration.cs` (HKCU Run key).
  - `Updates/`: `UpdateChecker.cs` (GitHub API), `UpdateService.cs` (daily schedule, notify once), `ReleaseInfo.cs`.
  - `Infrastructure/`: `AppInfo.cs` (name, repo `skezze/ChargeHub`, version), `Log.cs` (`chargehub.log`, rotates ~512 KB), `Strings.cs` (all localized UI text, EN + RU), `DiagnosticReport.cs`, `MessageWindow.cs` (hidden window for power and device-change messages).
  - `App/app.manifest`, `App/Probe.cs`.
- `tests/ChargeHub.Tests/`: xUnit, one file per area. Covers everything that does not need hardware. Internals are visible via `InternalsVisibleTo`.
- `installer/ChargeHub.iss`: Inno Setup 6 script. Installs the framework-dependent build and downloads/installs the .NET 10 Desktop Runtime only if `Microsoft.WindowsDesktop.App\10.*` is missing.
- `scripts/build.ps1`: test, publish (`-SelfContained` optional), `-Installer` builds the setup exe. `-SkipTests` skips tests.
- `.github/workflows/build.yml`: every push and PR to `main`, builds the installer. `release.yml`: on tag `v*`, checks tag == `<Version>`, publishes setup exe and portable zip.
- `assets/ChargeHub.ico`: app and installer icon.

## Commands

- Test: `dotnet test ChargeHub.sln -c Release`
- Build + tests + installer: `powershell -NoProfile -ExecutionPolicy Bypass -File scriptsuild.ps1 -Installer` (output in `artifacts/`)
- Quick app build: `dotnet build src/ChargeHub/ChargeHub.csproj`

## Conventions

- `Nullable` enabled, `ImplicitUsings` enabled, `TreatWarningsAsErrors` is ON. Warnings break the build.
- File-scoped namespaces. Most types are `internal sealed`. Records and primary constructors are used. Braces on their own lines.
- Pure logic (`TrayPlanner`, `DeviceBook`, `AsusProtocol`, `LowBatteryNotifier`) must stay free of Windows I/O so it can be unit-tested. Put hardware access in `Sources/` or `Tray/` host classes.
- Any user-visible text goes into `Infrastructure/Strings.cs` in both languages.
- Device ids: a device reachable through two sources must use the same id (`KnownDevices`) to be merged.
- Adding a device: implement `IBatterySource`, register it in `TrayApp`.
- Releases: bump `<Version>` in the csproj, commit, tag `vX.Y.Z`, push the tag. The release workflow fails if they differ.

## Rules and gotchas

- ASUS protocol: only the read-only battery query (`00 12 07`) may be sent. Never add writes (DPI, RGB, firmware, profiles).
- Do not read, print or commit `settings.json` or `chargehub.log`. They contain local Bluetooth addresses. Diagnostics without ids come from `DiagnosticReport.Build(..., includeIds: false)`.
- Ignore generated folders: `bin/`, `obj/`, `artifacts/`, `.vs/`, `tests/**/bin|obj`.
- A running ChargeHub (from `artifacts/win-x64`) locks its DLLs, so `scripts/build.ps1` fails at the cleanup step. Ask the user to exit it from the tray first. Do not kill it silently.
- Hardware behaviour (real mice, dongles, drivers) cannot be tested here. Say so instead of claiming it works.
- From Git Bash, pass Inno Setup switches with `MSYS_NO_PATHCONV=1`, otherwise `/D...` gets rewritten as a path.
