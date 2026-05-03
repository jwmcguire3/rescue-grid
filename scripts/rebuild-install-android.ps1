param(
    [string]$UnityExe = "",
    [string]$AdbExe = "",
    [string]$OutputRoot = "",
    [string]$ApplicationIdentifier = "com.defaultcompany.rescuegrid.dev",
    [string]$OutputName = "rescue-grid-android-dev.apk",
    [switch]$SkipTests,
    [switch]$UninstallFirst,
    [switch]$Launch,
    [switch]$ReleaseLike,
    [int]$BuildWaitSeconds = 900
)

$ErrorActionPreference = "Stop"

function Resolve-Executable {
    param(
        [string]$RequestedPath,
        [string]$EnvironmentVariableName,
        [string]$DefaultPath,
        [string]$CommandName,
        [string]$DisplayName
    )

    if ($RequestedPath -and (Test-Path -LiteralPath $RequestedPath)) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    $environmentPath = [Environment]::GetEnvironmentVariable($EnvironmentVariableName)
    if ($environmentPath -and (Test-Path -LiteralPath $environmentPath)) {
        return (Resolve-Path -LiteralPath $environmentPath).Path
    }

    if ($DefaultPath -and (Test-Path -LiteralPath $DefaultPath)) {
        return $DefaultPath
    }

    $command = Get-Command $CommandName -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw "Unable to find $DisplayName. Pass the path explicitly or set $EnvironmentVariableName."
}

function Resolve-AdbExe {
    param(
        [string]$RequestedPath,
        [string]$UnityPath
    )

    if ($RequestedPath -and (Test-Path -LiteralPath $RequestedPath)) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    if ($env:ADB_EXE -and (Test-Path -LiteralPath $env:ADB_EXE)) {
        return (Resolve-Path -LiteralPath $env:ADB_EXE).Path
    }

    $candidateRoots = @(
        $env:ANDROID_SDK_ROOT,
        $env:ANDROID_HOME,
        "C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK"
    ) | Where-Object { $_ }

    if ($env:LOCALAPPDATA) {
        $candidateRoots += Join-Path $env:LOCALAPPDATA "Android\Sdk"
    }

    if ($UnityPath) {
        $unityEditorDirectory = Split-Path -Parent $UnityPath
        $candidateRoots += Join-Path $unityEditorDirectory "Data\PlaybackEngines\AndroidPlayer\SDK"
    }

    foreach ($root in $candidateRoots) {
        $candidate = Join-Path $root "platform-tools\adb.exe"
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $command = Get-Command "adb" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw "Unable to find adb. Pass -AdbExe, set ADB_EXE, set ANDROID_SDK_ROOT / ANDROID_HOME, or install Android platform-tools."
}

function Invoke-ProcessChecked {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$FailureMessage
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE."
    }
}

function Invoke-UnityBuild {
    param(
        [string]$UnityPath,
        [string]$ProjectPath,
        [string]$LogPath
    )

    $arguments = @(
        "-batchmode",
        "-nographics",
        "-projectPath", $ProjectPath,
        "-buildTarget", "Android",
        "-executeMethod", "BuildScripts.BuildAndroid",
        "-logFile", $LogPath,
        "-silent-crashes",
        "-quit"
    )

    Invoke-ProcessChecked `
        -FilePath $UnityPath `
        -Arguments $arguments `
        -FailureMessage "Unity Android build failed. See $LogPath."
}

function Get-Sha256Hex {
    param(
        [byte[]]$Bytes
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.BitConverter]::ToString($sha256.ComputeHash($Bytes)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Assert-ApkLevelAssetsMatchSource {
    param(
        [string]$ApkPath,
        [string]$ProjectPath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $levelsDirectory = Join-Path $ProjectPath "Assets\StreamingAssets\Levels"
    $sourceFiles = Get-ChildItem -LiteralPath $levelsDirectory -Filter "*.json" -File
    if ($sourceFiles.Count -eq 0) {
        throw "No source level JSON files were found under $levelsDirectory."
    }

    $zip = [System.IO.Compression.ZipFile]::OpenRead($ApkPath)
    try {
        foreach ($sourceFile in $sourceFiles) {
            $entryName = "assets/Levels/$($sourceFile.Name)"
            $entry = $zip.GetEntry($entryName)
            if ($entry -eq $null) {
                throw "APK is missing StreamingAssets level $entryName."
            }

            $sourceBytes = [System.IO.File]::ReadAllBytes($sourceFile.FullName)
            $entryStream = $entry.Open()
            try {
                $memory = New-Object System.IO.MemoryStream
                try {
                    $entryStream.CopyTo($memory)
                    $entryBytes = $memory.ToArray()
                }
                finally {
                    $memory.Dispose()
                }
            }
            finally {
                $entryStream.Dispose()
            }

            $sourceHash = Get-Sha256Hex -Bytes $sourceBytes
            $entryHash = Get-Sha256Hex -Bytes $entryBytes
            if ($sourceHash -ne $entryHash) {
                throw "APK level asset mismatch for $($sourceFile.Name). Source and embedded StreamingAssets differ."
            }
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Wait-ForFreshApk {
    param(
        [string]$ApkPath,
        [datetime]$BuildStartedAt,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $apk = Get-Item -LiteralPath $ApkPath -ErrorAction SilentlyContinue
        if ($apk -and $apk.LastWriteTime -ge $BuildStartedAt) {
            return
        }

        Start-Sleep -Seconds 2
    }

    throw "Expected fresh APK was not found at $ApkPath within $TimeoutSeconds seconds."
}

$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$resolvedOutputRoot = if ($OutputRoot) {
    $OutputRoot
}
elseif ($env:RESCUE_BUILD_OUTPUT_ROOT) {
    $env:RESCUE_BUILD_OUTPUT_ROOT
}
else {
    Join-Path $projectPath "Build"
}

if ([System.IO.Path]::IsPathRooted($resolvedOutputRoot)) {
    $resolvedOutputRoot = [System.IO.Path]::GetFullPath($resolvedOutputRoot)
}
else {
    $resolvedOutputRoot = [System.IO.Path]::GetFullPath((Join-Path $projectPath $resolvedOutputRoot))
}
$logDirectory = Join-Path $resolvedOutputRoot "Logs"
$logPath = Join-Path $logDirectory "build-android.log"
$apkPath = Join-Path (Join-Path $resolvedOutputRoot "Android") $OutputName

$unityPath = Resolve-Executable `
    -RequestedPath $UnityExe `
    -EnvironmentVariableName "UNITY_EXE" `
    -DefaultPath "C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe" `
    -CommandName "Unity" `
    -DisplayName "Unity.exe"

if (-not $AdbExe -and -not $env:ADB_EXE) {
    $unityEmbeddedAdb = Join-Path (Split-Path -Parent $unityPath) "Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
    if (Test-Path -LiteralPath $unityEmbeddedAdb) {
        $AdbExe = $unityEmbeddedAdb
    }
}

$adbPath = Resolve-AdbExe -RequestedPath $AdbExe -UnityPath $unityPath

New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

if (-not $SkipTests) {
    Write-Host "Running EditMode tests..."
    & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "test.ps1") -Platforms EditMode -UnityExe $unityPath
    if ($LASTEXITCODE -ne 0) {
        throw "EditMode tests failed. Android rebuild was not started."
    }
}

Write-Host "Checking adb device connection..."
Invoke-ProcessChecked -FilePath $adbPath -Arguments @("get-state") -FailureMessage "No authorized adb device was found."

if (Test-Path -LiteralPath $apkPath) {
    Remove-Item -LiteralPath $apkPath -Force
}

if (Test-Path -LiteralPath $logPath) {
    Remove-Item -LiteralPath $logPath -Force
}

$env:RESCUE_BUILD_OUTPUT_ROOT = $resolvedOutputRoot
$env:RESCUE_ANDROID_FORMAT = "apk"
$env:RESCUE_ANDROID_OUTPUT_NAME = $OutputName
$env:RESCUE_ANDROID_APPLICATION_IDENTIFIER = $ApplicationIdentifier
$env:RESCUE_DEVELOPMENT_BUILD = if ($ReleaseLike) { "0" } else { "1" }
$env:RESCUE_ALLOW_DEBUGGING = if ($ReleaseLike) { "0" } else { "1" }

Write-Host "Building Android APK..."
$buildStartedAt = Get-Date
Invoke-UnityBuild -UnityPath $unityPath -ProjectPath $projectPath -LogPath $logPath

Wait-ForFreshApk -ApkPath $apkPath -BuildStartedAt $buildStartedAt -TimeoutSeconds $BuildWaitSeconds

Write-Host "Verifying APK level assets..."
Assert-ApkLevelAssetsMatchSource -ApkPath $apkPath -ProjectPath $projectPath

if ($UninstallFirst) {
    Write-Host "Uninstalling $ApplicationIdentifier before install..."
    & $adbPath uninstall $ApplicationIdentifier | Write-Host
}

Write-Host "Installing APK with adb..."
& $adbPath install -r $apkPath
if ($LASTEXITCODE -ne 0) {
    Write-Warning "adb install -r failed. Trying uninstall then clean install for $ApplicationIdentifier."
    & $adbPath uninstall $ApplicationIdentifier | Write-Host
    Invoke-ProcessChecked -FilePath $adbPath -Arguments @("install", $apkPath) -FailureMessage "adb clean install failed."
}

if ($Launch) {
    Write-Host "Launching $ApplicationIdentifier..."
    Invoke-ProcessChecked `
        -FilePath $adbPath `
        -Arguments @("shell", "monkey", "-p", $ApplicationIdentifier, "-c", "android.intent.category.LAUNCHER", "1") `
        -FailureMessage "adb launch failed."
}

Write-Host "Installed Android build: $apkPath"
