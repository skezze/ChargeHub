param(
    [switch]$SelfContained,
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

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
