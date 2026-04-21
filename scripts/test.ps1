param(
    [ValidateSet("EditMode", "PlayMode")]
    [string[]]$Platforms = @("EditMode"),
    [int]$DelaySeconds = 2,
    [string]$UnityExe = ""
)

$ErrorActionPreference = "Stop"

function Resolve-UnityExe {
    param(
        [string]$RequestedPath
    )

    if ($RequestedPath -and (Test-Path -LiteralPath $RequestedPath)) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    if ($env:UNITY_EXE -and (Test-Path -LiteralPath $env:UNITY_EXE)) {
        return (Resolve-Path -LiteralPath $env:UNITY_EXE).Path
    }

    $defaultPath = "C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe"
    if (Test-Path -LiteralPath $defaultPath) {
        return $defaultPath
    }

    throw "Unable to find Unity.exe. Pass -UnityExe or set UNITY_EXE."
}

function Invoke-UnityTests {
    param(
        [string]$UnityPath,
        [string]$ProjectPath,
        [string]$Platform
    )

    $prefix = $Platform.ToLowerInvariant()
    $resultsPath = Join-Path $ProjectPath "$prefix-results.xml"
    $logPath = Join-Path $ProjectPath "$prefix-tests.log"

    if (Test-Path -LiteralPath $resultsPath) {
        Remove-Item -LiteralPath $resultsPath -Force
    }

    if (Test-Path -LiteralPath $logPath) {
        Remove-Item -LiteralPath $logPath -Force
    }

    $arguments = @(
        "-batchmode",
        "-nographics",
        "-projectPath", $ProjectPath,
        "-runTests",
        "-testPlatform", $Platform,
        "-testResults", $resultsPath,
        "-logFile", $logPath
    )

    $exitCode = Invoke-UnityCommand -UnityPath $UnityPath -ProjectPath $ProjectPath -Arguments $arguments

    if (-not (Test-Path -LiteralPath $logPath)) {
        throw "Unity did not emit a log for $Platform."
    }

    if ($exitCode -ne 0) {
        throw "Unity exited with code $exitCode for $Platform. See $logPath."
    }

    if (-not (Test-Path -LiteralPath $resultsPath)) {
        Write-Warning "Unity completed $Platform without emitting $resultsPath on the first pass. Retrying once after the initial import/compile pass."
        $exitCode = Invoke-UnityCommand -UnityPath $UnityPath -ProjectPath $ProjectPath -Arguments $arguments

        if (-not (Test-Path -LiteralPath $logPath)) {
            throw "Unity did not emit a log for $Platform after retry."
        }

        if ($exitCode -ne 0) {
            throw "Unity exited with code $exitCode for $Platform on retry. See $logPath."
        }

        if (-not (Test-Path -LiteralPath $resultsPath)) {
            throw "Unity completed $Platform without emitting $resultsPath after retry. This usually means the invocation exited before the command-line test runner saved results."
        }
    }

    Write-Host "$Platform complete:"
    Write-Host "  Results: $resultsPath"
    Write-Host "  Log: $logPath"
}

function Invoke-UnityCommand {
    param(
        [string]$UnityPath,
        [string]$ProjectPath,
        [string[]]$Arguments
    )

    Push-Location $ProjectPath
    try {
        $quotedArguments = foreach ($argument in $Arguments) {
            if ($argument.Contains(" ")) {
                '"' + $argument + '"'
            }
            else {
                $argument
            }
        }

        $commandLine = '"' + $UnityPath + '" ' + ($quotedArguments -join " ")
        cmd.exe /c $commandLine
        return $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}

$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$resolvedUnityExe = Resolve-UnityExe -RequestedPath $UnityExe

for ($i = 0; $i -lt $Platforms.Length; $i++) {
    Invoke-UnityTests -UnityPath $resolvedUnityExe -ProjectPath $projectPath -Platform $Platforms[$i]

    if ($i -lt ($Platforms.Length - 1) -and $DelaySeconds -gt 0) {
        Start-Sleep -Seconds $DelaySeconds
    }
}
