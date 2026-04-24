param(
    [string]$CaptureAppPath = "",
    [string]$PlayerLogPath = "",
    [string]$ReportPath = ""
)

$ErrorActionPreference = "Stop"

$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($CaptureAppPath)) {
    $CaptureAppPath = Join-Path $projectPath "Build/Capture/Windows/rescue-grid-capture.exe"
}

if (-not (Test-Path -LiteralPath $CaptureAppPath)) {
    throw "Capture app not found at '$CaptureAppPath'. Build it first."
}

if ([string]::IsNullOrWhiteSpace($PlayerLogPath)) {
    $PlayerLogPath = Join-Path $projectPath "Build/Logs/verify-capture-player.log"
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $projectPath "Build/Logs/L15.capture.json"
}

$playerLogDirectory = Split-Path -Parent $PlayerLogPath
$reportDirectory = Split-Path -Parent $ReportPath

if ([string]::IsNullOrWhiteSpace($playerLogDirectory) -or [string]::IsNullOrWhiteSpace($reportDirectory)) {
    throw "Player log path and report path must include a directory."
}

New-Item -ItemType Directory -Force -Path $playerLogDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null

Get-Process -Name "rescue-grid-capture" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

if (Test-Path -LiteralPath $PlayerLogPath) {
    Remove-Item -LiteralPath $PlayerLogPath -Force
}

if (Test-Path -LiteralPath $ReportPath) {
    Remove-Item -LiteralPath $ReportPath -Force
}

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $CaptureAppPath
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.WorkingDirectory = Split-Path -Parent $CaptureAppPath
$startInfo.Arguments = "-batchmode -nographics -capture-l15 -capture-exit -capture-report-path `"$ReportPath`" -logFile `"$PlayerLogPath`""

$process = [System.Diagnostics.Process]::Start($startInfo)
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {
    if ($process.HasExited) {
        break
    }

    if (Test-Path -LiteralPath $ReportPath) {
        break
    }

    Start-Sleep -Milliseconds 250
}

if (-not (Test-Path -LiteralPath $ReportPath)) {
    if ($process.HasExited) {
        throw "Capture player exited with code $($process.ExitCode) before writing '$ReportPath'. See $PlayerLogPath."
    }

    try {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    catch {
    }

    throw "Capture player did not write '$ReportPath' within 30 seconds. See $PlayerLogPath."
}

$graceDeadline = (Get-Date).AddSeconds(5)
while (-not $process.HasExited -and (Get-Date) -lt $graceDeadline) {
    Start-Sleep -Milliseconds 250
    $process.Refresh()
}

if (-not $process.HasExited) {
    Stop-Process -Id $process.Id -Force -ErrorAction Stop
}
elseif ($process.ExitCode -ne 0) {
    throw "Capture player exited with code $($process.ExitCode). See $PlayerLogPath."
}

if (-not (Test-Path -LiteralPath $ReportPath)) {
    throw "Capture player completed without writing the report at '$ReportPath'."
}

$report = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json

if ($report.LevelId -ne "L15") {
    throw "Expected LevelId 'L15' but found '$($report.LevelId)'."
}

if ($report.Seed -ne 5) {
    throw "Expected Seed 5 but found '$($report.Seed)'."
}

if ($report.Outcome -ne "Win") {
    throw "Expected Outcome 'Win' but found '$($report.Outcome)'."
}

if ($null -eq $report.Steps -or $report.Steps.Count -ne 2) {
    throw "Expected exactly 2 capture steps but found '$($report.Steps.Count)'."
}

Write-Host "Capture verification passed:"
Write-Host "  App: $CaptureAppPath"
Write-Host "  Report: $ReportPath"
Write-Host "  Player log: $PlayerLogPath"
