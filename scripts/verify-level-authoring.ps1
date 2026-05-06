param(
    [string]$ProjectPath = "",
    [switch]$ContinueOnError
)

$ErrorActionPreference = "Stop"

function Resolve-ProjectPath {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
}

function New-Stage {
    param(
        [string]$Name,
        [string[]]$Command,
        [string]$DetailsCommand,
        [scriptblock]$ShouldSkip = $null,
        [string]$SkipReason = ""
    )

    return [pscustomobject]@{
        Name = $Name
        Command = $Command
        DetailsCommand = $DetailsCommand
        ShouldSkip = $ShouldSkip
        SkipReason = $SkipReason
    }
}

function Format-Command {
    param([string[]]$Command)

    return $Command -join " "
}

function Count-Warnings {
    param([string[]]$Lines)

    $count = 0
    foreach ($line in $Lines) {
        if ($line -match "(?i)\bwarning\b" -or $line -match "(?i)\bwarn\b") {
            $count++
        }
    }

    return $count
}

function Invoke-GateStage {
    param(
        [pscustomobject]$Stage,
        [string]$LogDirectory
    )

    if ($null -ne $Stage.ShouldSkip -and (& $Stage.ShouldSkip)) {
        Write-Host ""
        Write-Host "== $($Stage.Name) =="
        Write-Host "SKIP: $($Stage.SkipReason)"
        return [pscustomobject]@{
            Name = $Stage.Name
            Status = "PASS"
            ExitCode = 0
            WarningCount = 0
            DetailsCommand = $Stage.DetailsCommand
            Note = $Stage.SkipReason
        }
    }

    $logPath = Join-Path $LogDirectory (($Stage.Name -replace "[^A-Za-z0-9._-]", "_") + ".log")
    $commandText = Format-Command -Command $Stage.Command

    Write-Host ""
    Write-Host "== $($Stage.Name) =="
    Write-Host "> $commandText"

    $stageOutput = New-Object System.Collections.Generic.List[string]
    & $Stage.Command[0] $Stage.Command[1..($Stage.Command.Length - 1)] 2>&1 |
        ForEach-Object {
            $stageOutput.Add($_.ToString())
            $_
        } |
        Tee-Object -FilePath $logPath
    $exitCode = $LASTEXITCODE
    if ($null -eq $exitCode) {
        $exitCode = 0
    }

    $warningCount = Count-Warnings -Lines $stageOutput.ToArray()
    $status = if ($exitCode -eq 0) { "PASS" } else { "FAIL" }

    return [pscustomobject]@{
        Name = $Stage.Name
        Status = $status
        ExitCode = $exitCode
        WarningCount = $warningCount
        DetailsCommand = $Stage.DetailsCommand
        Note = "log: $logPath"
    }
}

function Write-Summary {
    param([object[]]$Results)

    Write-Host ""
    Write-Host "Level authoring gate summary"
    Write-Host "Stage                         Status  Warnings  Inspect"
    Write-Host "-----                         ------  --------  -------"

    foreach ($result in $Results) {
        Write-Host ("{0,-29} {1,-7} {2,-9} {3}" -f $result.Name, $result.Status, $result.WarningCount, $result.DetailsCommand)
    }

    $failed = @($Results | Where-Object { $_.Status -eq "FAIL" })
    Write-Host ""
    if ($failed.Count -eq 0) {
        Write-Host "Level authoring gate passed."
    }
    else {
        Write-Host "Level authoring gate failed: $($failed.Name -join ', ')"
    }
}

$projectPath = Resolve-ProjectPath -RequestedPath $ProjectPath
$levelsDirectory = Join-Path $projectPath "Assets\StreamingAssets\Levels"
$resourcesDirectory = Join-Path $projectPath "Assets\Resources\Levels"
$briefDirectory = Join-Path $projectPath "docs\level-briefs"
$manifestPath = Join-Path $projectPath "docs\level-packets\phase1.packet.json"
$logDirectory = Join-Path $projectPath "Reports\LevelAuthoringGate"

Push-Location $projectPath
try {
    if (-not (Test-Path -LiteralPath $levelsDirectory)) {
        throw "Required levels directory was not found: $levelsDirectory"
    }

    if (-not (Test-Path -LiteralPath $briefDirectory)) {
        throw "Required briefs directory was not found: $briefDirectory"
    }

    if (-not (Test-Path -LiteralPath $resourcesDirectory)) {
        throw "Required resources directory was not found: $resourcesDirectory"
    }

    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "Required packet manifest was not found: $manifestPath"
    }

    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

    $hasFailPaths = (Get-ChildItem -LiteralPath $resourcesDirectory -Filter "*.fail.json" -File).Count -gt 0
    $stages = @(
        (New-Stage `
            -Name "validate-all" `
            -Command @("dotnet", "run", "--project", "Tools/LevelValidator/LevelValidator.csproj", "--", "validate-all", "Assets/StreamingAssets/Levels") `
            -DetailsCommand "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-all Assets/StreamingAssets/Levels"),
        (New-Stage `
            -Name "validate-phase1-all" `
            -Command @("dotnet", "run", "--project", "Tools/LevelValidator/LevelValidator.csproj", "--", "validate-phase1-all", "Assets/StreamingAssets/Levels") `
            -DetailsCommand "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-phase1-all Assets/StreamingAssets/Levels"),
        (New-Stage `
            -Name "validate-brief-all" `
            -Command @("dotnet", "run", "--project", "Tools/LevelValidator/LevelValidator.csproj", "--", "validate-brief-all", "Assets/StreamingAssets/Levels", "docs/level-briefs") `
            -DetailsCommand "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- validate-brief-all Assets/StreamingAssets/Levels docs/level-briefs"),
        (New-Stage `
            -Name "readability-all" `
            -Command @("dotnet", "run", "--project", "Tools/LevelValidator/LevelValidator.csproj", "--", "readability-all", "Assets/StreamingAssets/Levels", "docs/level-briefs") `
            -DetailsCommand "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- readability-all Assets/StreamingAssets/Levels docs/level-briefs"),
        (New-Stage `
            -Name "design-report-all" `
            -Command @("dotnet", "run", "--project", "Tools/LevelValidator/LevelValidator.csproj", "--", "design-report-all", "Assets/StreamingAssets/Levels", "docs/level-briefs") `
            -DetailsCommand "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- design-report-all Assets/StreamingAssets/Levels docs/level-briefs"),
        (New-Stage `
            -Name "packet-design-report" `
            -Command @("dotnet", "run", "--project", "Tools/LevelValidator/LevelValidator.csproj", "--", "packet-report", "docs/level-packets/phase1.packet.json", "Assets/StreamingAssets/Levels", "docs/level-briefs") `
            -DetailsCommand "dotnet run --project Tools/LevelValidator/LevelValidator.csproj -- packet-report docs/level-packets/phase1.packet.json Assets/StreamingAssets/Levels docs/level-briefs"),
        (New-Stage `
            -Name "verify-solves" `
            -Command @("dotnet", "run", "--project", "Tools/SolveAuthoring/SolveAuthoring.csproj", "--", "--verify-solves") `
            -DetailsCommand "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-solves"),
        (New-Stage `
            -Name "verify-golden" `
            -Command @("dotnet", "run", "--project", "Tools/SolveAuthoring/SolveAuthoring.csproj", "--", "--verify-golden") `
            -DetailsCommand "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-golden"),
        (New-Stage `
            -Name "verify-failpaths" `
            -Command @("dotnet", "run", "--project", "Tools/SolveAuthoring/SolveAuthoring.csproj", "--", "--verify-failpaths") `
            -DetailsCommand "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-failpaths" `
            -ShouldSkip { -not $hasFailPaths } `
            -SkipReason "No .fail.json files found under Assets/Resources/Levels."),
        (New-Stage `
            -Name "compare-assistance-all" `
            -Command @("dotnet", "run", "--project", "Tools/SolveAuthoring/SolveAuthoring.csproj", "--", "--compare-assistance-all") `
            -DetailsCommand "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --compare-assistance-all"),
        (New-Stage `
            -Name "packet-report" `
            -Command @("dotnet", "run", "--project", "Tools/LevelTelemetry/LevelTelemetry.csproj", "--", "summarize-all") `
            -DetailsCommand "dotnet run --project Tools/LevelTelemetry/LevelTelemetry.csproj -- summarize-all"),
        (New-Stage `
            -Name "verify-acceptance" `
            -Command @("dotnet", "run", "--project", "Tools/SolveAuthoring/SolveAuthoring.csproj", "--", "--verify-acceptance", "--manifest", "docs/level-packets/phase1.packet.json", "--levels-dir", "Assets/StreamingAssets/Levels", "--briefs-dir", "docs/level-briefs", "--resources-dir", "Assets/Resources/Levels") `
            -DetailsCommand "dotnet run --project Tools/SolveAuthoring/SolveAuthoring.csproj -- --verify-acceptance --manifest docs/level-packets/phase1.packet.json --levels-dir Assets/StreamingAssets/Levels --briefs-dir docs/level-briefs --resources-dir Assets/Resources/Levels")
    )

    Write-Host "Verifying Phase 1 level packet for design review / playtest build..."
    Write-Host "Project: $projectPath"
    Write-Host "Manifest: docs/level-packets/phase1.packet.json"

    $results = New-Object System.Collections.Generic.List[object]
    foreach ($stage in $stages) {
        $result = Invoke-GateStage -Stage $stage -LogDirectory $logDirectory
        $results.Add($result)

        if ($result.Status -eq "FAIL" -and -not $ContinueOnError) {
            Write-Summary -Results $results.ToArray()
            exit $result.ExitCode
        }
    }

    Write-Summary -Results $results.ToArray()
    $failed = @($results | Where-Object { $_.Status -eq "FAIL" })
    if ($failed.Count -gt 0) {
        exit 1
    }
}
finally {
    Pop-Location
}
