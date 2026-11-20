param(
    [switch]$SelfContained,
    [switch]$SkipTests,
    [switch]$Installer
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if ($Installer -and $SelfContained) { throw 'The installer packages the framework-dependent build; do not combine -Installer with -SelfContained' }

if (-not $SkipTests) {
    dotnet test ChargeHub.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}

$destination = Join-Path $PSScriptRoot 'artifacts\win-x64'
if (Test-Path $destination) { Remove-Item $destination -Recurse -Force }

dotnet publish src/ChargeHub/ChargeHub.csproj -c Release -r win-x64 `
    --self-contained $SelfContained.IsPresent.ToString().ToLowerInvariant() `
    -o $destination
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }

Copy-Item LICENSE, THIRD_PARTY_NOTICES.md, README.md $destination -Force
Write-Host "Ready: $destination\ChargeHub.exe"

if ($Installer) {
    $version = ([xml](Get-Content src/ChargeHub/ChargeHub.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    $iscc = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
        Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
        Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $iscc) { throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup' }

    & $iscc "/DAppVersion=$version" installer\ChargeHub.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
    Write-Host "Ready: $PSScriptRoot\artifacts\installer\ChargeHub-$version-setup.exe"
}
