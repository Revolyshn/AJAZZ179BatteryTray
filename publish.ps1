# Runs the tests, publishes the single-file EXE and copies it to the repository root
# (the location autostart points to). A copy running from the root is renamed to
# AJ179BatteryTray.old.exe (Windows allows renaming a running EXE); restart the app to
# switch to the new build. The .old file is removed by the next publish.
param([switch]$SkipTests)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src\AJ179BatteryTray\AJ179BatteryTray.csproj'
$publishDir = Join-Path $root 'artifacts\publish'
$target = Join-Path $root 'AJ179BatteryTray.exe'
$old = Join-Path $root 'AJ179BatteryTray.old.exe'

if (-not $SkipTests) {
    dotnet test (Join-Path $root 'AJ179BatteryTray.slnx') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}

dotnet publish $project -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }

if (Test-Path -LiteralPath $old) {
    try { Remove-Item -LiteralPath $old -Force }
    catch { throw "$old is still running. Exit the app from the tray menu and rerun." }
}

$running = Get-Process AJ179BatteryTray -ErrorAction SilentlyContinue | Where-Object Path -eq $target
if ($running) {
    Move-Item -LiteralPath $target -Destination $old
    Write-Warning 'The running copy was renamed to AJ179BatteryTray.old.exe. Exit it from the tray menu and start AJ179BatteryTray.exe again.'
}

Copy-Item (Join-Path $publishDir 'AJ179BatteryTray.exe') $target -Force
Copy-Item (Join-Path $root 'src\AJ179BatteryTray\Assets\JetBrainsMono-OFL.txt') $root -Force
Get-Item -LiteralPath $target | Select-Object Name, Length, LastWriteTime
