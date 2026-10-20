# Runs the tests, publishes the single-file EXE and copies it to the repository root
# (the location autostart points to). A copy running from the root is renamed to
# AJ179BatteryTray.old.exe (Windows allows renaming a running EXE); restart the app to
# switch to the new build. The .old file is removed by the next publish.
#
#   SelfContained       ~50 MB, runs on any Windows 10/11 x64 (default)
#   FrameworkDependent  ~1.3 MB, needs the .NET 10 Desktop Runtime (x64) installed
param(
    [ValidateSet('SelfContained', 'FrameworkDependent')]
    [string]$Variant = 'SelfContained',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src\AJ179BatteryTray\AJ179BatteryTray.csproj'
$publishDir = Join-Path $root "artifacts\publish\$($Variant.ToLowerInvariant())"
$selfContained = ($Variant -eq 'SelfContained').ToString().ToLowerInvariant()
$target = Join-Path $root 'AJ179BatteryTray.exe'
$old = Join-Path $root 'AJ179BatteryTray.old.exe'

if (-not $SkipTests) {
    dotnet test (Join-Path $root 'AJ179BatteryTray.slnx') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}

dotnet publish $project -c Release -p:SelfContained=$selfContained -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }

if (Test-Path -LiteralPath $old) {
    try { Remove-Item -LiteralPath $old -Force }
    catch { throw "$old is still running. Exit the app from the tray menu and rerun." }
}

# A running EXE cannot be opened for writing; the process path alone is not reliable
# because it keeps reporting the original name after a rename.
function Test-Locked([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $false }
    try { [IO.File]::Open($Path, 'Open', 'ReadWrite', 'None').Dispose(); return $false }
    catch [IO.IOException] { return $true }
}

if (Test-Locked $target) {
    Move-Item -LiteralPath $target -Destination $old
    Write-Warning 'The running copy was renamed to AJ179BatteryTray.old.exe. Exit it from the tray menu and start AJ179BatteryTray.exe again.'
}

Copy-Item (Join-Path $publishDir 'AJ179BatteryTray.exe') $target -Force
Copy-Item (Join-Path $root 'src\AJ179BatteryTray\Assets\JetBrainsMono-OFL.txt') $root -Force
Get-Item -LiteralPath $target | Select-Object Name, Length, LastWriteTime
