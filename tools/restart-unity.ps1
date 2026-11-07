# Kills the Unity editor that has this project open, then starts it again.
# Does not print the process command line (that line can contain a Hub token).
param(
    [string]$ProjectPath = "C:\Users\georg\source\RiskASP",
    [string]$EditorPath = "C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe"
)

$ErrorActionPreference = "Stop"
$project = (Resolve-Path $ProjectPath).Path

$editors = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
    $_.ExecutablePath -and
    $_.ExecutablePath -like "*\Editor\Unity.exe" -and
    $_.CommandLine -and
    $_.CommandLine -like "*$project*"
}

$killed = @()
foreach ($editor in $editors) {
    Stop-Process -Id $editor.ProcessId -Force -ErrorAction SilentlyContinue
    $killed += $editor.ProcessId
}

$deadline = (Get-Date).AddSeconds(45)
while ((Get-Date) -lt $deadline) {
    $still = Get-Process -Name Unity -ErrorAction SilentlyContinue | Where-Object { $killed -contains $_.Id }
    if (-not $still) { break }
    Start-Sleep -Seconds 1
}

$lock = Join-Path $project "Temp\UnityLockfile"
if (Test-Path $lock) {
    Remove-Item $lock -Force -ErrorAction SilentlyContinue
}

# A crashed editor leaves Temp\__Backupscenes, and the next launch asks
# "Recovering Scene Backups?" before the project opens. This version of Unity
# has no preference to hide that question. Removing the leftovers skips it.
$backups = Join-Path $project "Temp\__Backupscenes"
if (Test-Path $backups) {
    Remove-Item $backups -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path $EditorPath)) {
    throw "Unity editor not found at $EditorPath"
}

$started = Start-Process -FilePath $EditorPath -ArgumentList @("-projectPath", $project) -PassThru
Write-Output ("killed=" + ($(if ($killed.Count) { $killed -join "," } else { "none" })) + " started=" + $started.Id)
