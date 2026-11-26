; ChargeHub installer (Inno Setup 6.3+). Build with: ./scripts/build.ps1 -Installer
; The app is packaged framework-dependent. If the .NET 10 Desktop Runtime is missing,
; the installer downloads the official Microsoft runtime and installs it silently.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{6E0C4B2A-9F3D-4C7E-8A1B-5D2F9E7C3A10}
AppName=ChargeHub
AppVersion={#AppVersion}
AppVerName=ChargeHub {#AppVersion}
AppPublisher=skezze
AppPublisherURL=https://github.com/skezze/ChargeHub
AppSupportURL=https://github.com/skezze/ChargeHub/issues
AppUpdatesURL=https://github.com/skezze/ChargeHub/releases
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\ChargeHub
DefaultGroupName=ChargeHub
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=ChargeHub-{#AppVersion}-setup
SetupIconFile=..\assets\ChargeHub.ico
UninstallDisplayIcon={app}\ChargeHub.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ChargeHub"; Filename: "{app}\ChargeHub.exe"
Name: "{group}\{cm:UninstallProgram,ChargeHub}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ChargeHub"; Filename: "{app}\ChargeHub.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ChargeHub.exe"; Description: "{cm:LaunchProgram,ChargeHub}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\ChargeHub.exe"; Parameters: "--background"; Flags: nowait runasoriginaluser; Check: IsInAppUpdate

[Code]
const
  RuntimeUrl = 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe';
  RuntimePage = 'https://dotnet.microsoft.com/download/dotnet/10.0';

function Tr(const English, Russian: String): String;
begin
  if ActiveLanguage = 'russian' then Result := Russian else Result := English;
end;

function DesktopRuntimeInstalled: Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), FindRec);
  if Result then FindClose(FindRec);
end;

function ManualInstallHint: String;
begin
  Result := Tr(' Install it manually from ', ' Установите его вручную по ссылке ') + RuntimePage +
    Tr(' and run the setup again.', ' и запустите установщик снова.');
end;

function InstallDesktopRuntime(var NeedsRestart: Boolean): String;
var
  Installer: String;
  ResultCode: Integer;
begin
  Result := '';
  Installer := ExpandConstant('{tmp}\windowsdesktop-runtime-win-x64.exe');

  WizardForm.StatusLabel.Caption := Tr('Downloading the .NET 10 Desktop Runtime...', 'Загрузка .NET 10 Desktop Runtime...');
  ResultCode := -1;
  if (not Exec(ExpandConstant('{sys}\curl.exe'), '-fsSL -o "' + Installer + '" "' + RuntimeUrl + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)) or (ResultCode <> 0) then
  begin
    Result := Tr('Could not download the .NET 10 Desktop Runtime. Check your internet connection.',
      'Не удалось скачать .NET 10 Desktop Runtime. Проверьте подключение к интернету.') + ManualInstallHint;
    Exit;
  end;

  WizardForm.StatusLabel.Caption := Tr('Installing the .NET 10 Desktop Runtime...', 'Установка .NET 10 Desktop Runtime...');
  ResultCode := -1;
  if (not Exec(Installer, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)) or
     ((ResultCode <> 0) and (ResultCode <> 3010) and (ResultCode <> 1638)) then
  begin
    Result := Format(Tr('The .NET 10 Desktop Runtime could not be installed (exit code %d).',
      'Не удалось установить .NET 10 Desktop Runtime (код %d).'), [ResultCode]) + ManualInstallHint;
    Exit;
  end;

  NeedsRestart := ResultCode = 3010;
  if not DesktopRuntimeInstalled then
    Result := Tr('The .NET 10 Desktop Runtime is still missing after its installation.',
      'После установки .NET 10 Desktop Runtime всё ещё не найден.') + ManualInstallHint;
end;

function OpenEvent(DesiredAccess: LongWord; InheritHandle: Boolean; Name: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(Event: THandle): Boolean;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

function IsInAppUpdate: Boolean;
begin
  Result := ExpandConstant('{param:ChargeHubUpdate|}') <> '';
end;

function InitializeSetup: Boolean;
var
  Attempt: Integer;
  Ready: THandle;
begin
  if IsInAppUpdate then
  begin
    { Confirm that the elevated installer is ready before the app shuts down. }
    Result := False;
    Ready := OpenEvent(2, False, 'Local\ChargeHub.Update.' + ExpandConstant('{param:ChargeHubUpdate|}'));
    if Ready = 0 then Exit;
    try
      if not SetEvent(Ready) then Exit;
    finally
      CloseHandle(Ready);
    end;
    for Attempt := 1 to 100 do
    begin
      if not CheckForMutexes('Local\ChargeHub.Tray') then Break;
      Sleep(100);
    end;
  end;
  Result := not CheckForMutexes('Local\ChargeHub.Tray');
  if not Result then
    SuppressibleMsgBox(Tr('Exit ChargeHub from the tray and try again.',
      'Закройте ChargeHub через трей и попробуйте снова.'), mbError, MB_OK, IDOK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not DesktopRuntimeInstalled then
    Result := InstallDesktopRuntime(NeedsRestart);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ChargeHub');
end;
