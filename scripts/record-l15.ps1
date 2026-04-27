param(
    [string]$CaptureAppPath = "",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$PlayerArguments = @()
)

$ErrorActionPreference = "Stop"

$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($CaptureAppPath)) {
    $CaptureAppPath = Join-Path $projectPath "Build/Capture/Windows/rescue-grid-capture.exe"
}

if (-not (Test-Path -LiteralPath $CaptureAppPath)) {
    throw "Capture app not found at '$CaptureAppPath'. Build it first with scripts/build-capture.sh."
}

$arguments = @("-capture-l15") + $PlayerArguments
Start-Process -FilePath $CaptureAppPath -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $CaptureAppPath)
