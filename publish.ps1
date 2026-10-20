# Runs the tests, publishes the single-file EXE and copies it to the repository root
# (the location autostart points to). Stop a running copy first: the EXE is locked while it runs.
param([switch]$SkipTests)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src\AJ179BatteryTray\AJ179BatteryTray.csproj'
$publishDir = Join-Path $root 'artifacts\publish'

if (-not $SkipTests) {
    dotnet test (Join-Path $root 'AJ179BatteryTray.slnx') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}

dotnet publish $project -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }

if (Get-Process AJ179BatteryTray -ErrorAction SilentlyContinue | Where-Object Path -eq (Join-Path $root 'AJ179BatteryTray.exe')) {
    throw 'AJ179BatteryTray.exe is running from the repository root. Exit it from the tray menu and rerun.'
}

Copy-Item (Join-Path $publishDir 'AJ179BatteryTray.exe') $root -Force
Copy-Item (Join-Path $root 'src\AJ179BatteryTray\Assets\JetBrainsMono-OFL.txt') $root -Force
Get-Item (Join-Path $root 'AJ179BatteryTray.exe') | Select-Object Name, Length, LastWriteTime
