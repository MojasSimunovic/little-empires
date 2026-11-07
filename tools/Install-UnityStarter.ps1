# Copy the authoritative C# core and Unity starter scene scripts into an
# existing Unity 6 Universal 3D project (on Windows).
#
# From the repository root:
#   powershell -ExecutionPolicy Bypass -File .\tools\Install-UnityStarter.ps1 -UnityProject "C:\Dev\LittleEmpires"
# Run again with -CoreOnly after game logic changes to re-sync the shared core.
param(
    [Parameter(Mandatory = $true)]
    [string]$UnityProject,
    [switch]$CoreOnly
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$unityRoot = [System.IO.Path]::GetFullPath($UnityProject)
$sourceCore = Join-Path $repo "src\LittleEmpires.Core"
$sourceStarter = Join-Path $repo "unity\starter\Assets\LittleEmpires"
$target = Join-Path $unityRoot "Assets\LittleEmpires"
$targetCore = Join-Path $target "Core"

if (-not (Test-Path (Join-Path $unityRoot "Packages\manifest.json"))) {
    throw "Not a Unity project: missing Packages/manifest.json in '$unityRoot'. In Unity Hub create a Universal 3D project first."
}
if (-not (Test-Path (Join-Path $unityRoot "Assets"))) {
    throw "Not a Unity project: missing Assets folder in '$unityRoot'."
}
if (-not (Test-Path (Join-Path $sourceCore "WorldGameSession.cs"))) {
    throw "Cannot find C# core under '$sourceCore'. Run this script from a full Git checkout."
}

if (-not $CoreOnly) {
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item -Path (Join-Path $sourceStarter "*") -Destination $target -Recurse -Force
    Write-Host "Copied Unity scene scripts and assembly definitions." -ForegroundColor Green
}

New-Item -ItemType Directory -Force -Path $targetCore | Out-Null
$sourceFiles = Get-ChildItem -Path $sourceCore -Filter "*.cs" -File
foreach ($file in $sourceFiles) {
    Copy-Item -Path $file.FullName -Destination (Join-Path $targetCore $file.Name) -Force
}
Write-Host ("Synced {0} authoritative C# core files into {1}" -f $sourceFiles.Count, $targetCore) -ForegroundColor Green
Write-Host ""
Write-Host "Next steps in Unity:" -ForegroundColor Cyan
Write-Host "1. Return to Unity and wait for scripts to compile."
Write-Host "2. Check Console for errors."
if (-not $CoreOnly) {
    Write-Host "3. Run Little Empires > Create Starter Island Scene from the top menu."
    Write-Host "4. Press Play to explore the generated island."
}
Write-Host "If Input errors appear: Player Settings > Active Input Handling > Both (restart Unity)."
