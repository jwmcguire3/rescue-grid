param(
    [string]$ProjectPath = ""
)

$ErrorActionPreference = "Stop"

function Resolve-ProjectPath {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
}

function Invoke-Checked {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$FailureMessage
    )

    Write-Host ""
    Write-Host "> $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw $FailureMessage
    }
}

function Get-IdsFromFiles {
    param(
        [string]$DirectoryPath,
        [string]$Pattern,
        [string]$SuffixToRemove
    )

    if (-not (Test-Path -LiteralPath $DirectoryPath)) {
        throw "Required directory was not found: $DirectoryPath"
    }

    $ids = New-Object System.Collections.Generic.List[string]
    Get-ChildItem -LiteralPath $DirectoryPath -Filter $Pattern -File |
        Sort-Object Name |
        ForEach-Object {
            $name = $_.Name
            if ([string]::IsNullOrEmpty($SuffixToRemove)) {
                $ids.Add($_.BaseName)
            }
            elseif ($name.EndsWith($SuffixToRemove, [StringComparison]::Ordinal)) {
                $ids.Add($name.Substring(0, $name.Length - $SuffixToRemove.Length))
            }
        }

    return $ids.ToArray()
}

function Assert-SameIds {
    param(
        [string[]]$Expected,
        [string[]]$Actual,
        [string]$MissingMessage,
        [string]$ExtraMessage
    )

    $expectedSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($id in $Expected) {
        [void]$expectedSet.Add($id)
    }

    $actualSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($id in $Actual) {
        [void]$actualSet.Add($id)
    }

    $missing = @($Expected | Where-Object { -not $actualSet.Contains($_) })
    $extra = @($Actual | Where-Object { -not $expectedSet.Contains($_) })

    if ($missing.Count -gt 0) {
        throw "$MissingMessage $($missing -join ', ')"
    }

    if ($extra.Count -gt 0) {
        throw "$ExtraMessage $($extra -join ', ')"
    }
}

$projectPath = Resolve-ProjectPath -RequestedPath $ProjectPath
$levelsDirectory = Join-Path $projectPath "Assets\StreamingAssets\Levels"
$resourcesDirectory = Join-Path $projectPath "Assets\Resources\Levels"
$briefDirectory = Join-Path $projectPath "docs\level-briefs"
$telemetryOutput = Join-Path $projectPath "Reports\LevelTelemetry\CiSmoke"

Push-Location $projectPath
try {
    Write-Host "Verifying level authoring coverage..."
    $levelIds = Get-IdsFromFiles -DirectoryPath $levelsDirectory -Pattern "*.json" -SuffixToRemove ".json"
    $solveIds = Get-IdsFromFiles -DirectoryPath $resourcesDirectory -Pattern "*.solve.json" -SuffixToRemove ".solve.json"
    $briefIds = Get-IdsFromFiles -DirectoryPath $briefDirectory -Pattern "*.brief.json" -SuffixToRemove ".brief.json"

    if ($levelIds.Count -eq 0) {
        throw "No level JSON files were found under $levelsDirectory."
    }

    Assert-SameIds `
        -Expected $levelIds `
        -Actual $solveIds `
        -MissingMessage "Missing solve files for level(s):" `
        -ExtraMessage "Solve files point at missing level(s):"

    Assert-SameIds `
        -Expected $levelIds `
        -Actual $briefIds `
        -MissingMessage "Missing level briefs for level(s):" `
        -ExtraMessage "Level briefs point at missing level(s):"

    Write-Host "Coverage OK for $($levelIds.Count) authored level(s)."

    Invoke-Checked `
        -FilePath "dotnet" `
        -Arguments @("run", "--project", "Tools/LevelValidator/LevelValidator.csproj", "--", "validate-all", "Assets/StreamingAssets/Levels") `
        -FailureMessage "LevelValidator validate-all failed."

    Invoke-Checked `
        -FilePath "dotnet" `
        -Arguments @("run", "--project", "Tools/LevelValidator/LevelValidator.csproj", "--", "validate-phase1-all", "Assets/StreamingAssets/Levels") `
        -FailureMessage "LevelValidator validate-phase1-all failed."

    Invoke-Checked `
        -FilePath "dotnet" `
        -Arguments @("run", "--project", "Tools/SolveAuthoring/SolveAuthoring.csproj", "--", "--verify-solves") `
        -FailureMessage "SolveAuthoring --verify-solves failed."

    Invoke-Checked `
        -FilePath "dotnet" `
        -Arguments @("run", "--project", "Tools/SolveAuthoring/SolveAuthoring.csproj", "--", "--verify-golden") `
        -FailureMessage "SolveAuthoring --verify-golden failed."

    Invoke-Checked `
        -FilePath "dotnet" `
        -Arguments @("run", "--project", "Tools/LevelTelemetry/LevelTelemetry.csproj", "--", "--range", "L00-L20", "--samples", "2", "--max-actions", "5", "--output", $telemetryOutput) `
        -FailureMessage "LevelTelemetry CI smoke failed."

    Invoke-Checked `
        -FilePath "dotnet" `
        -Arguments @("test", "Tools/LevelTelemetry.Tests/LevelTelemetry.Tests.csproj") `
        -FailureMessage "LevelTelemetry tests failed."

    Write-Host ""
    Write-Host "Level authoring gate passed."
}
finally {
    Pop-Location
}
