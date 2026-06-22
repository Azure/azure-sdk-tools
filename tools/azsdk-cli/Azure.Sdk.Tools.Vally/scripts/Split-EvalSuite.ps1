#Requires -Version 7.0

<#
.SYNOPSIS
    Discovers Vally eval files and emits an Azure Pipelines matrix for fan-out sharding.

.DESCRIPTION
    Vally has no `list` command, so scenario discovery is a filesystem glob of the same
    paths the suite uses. This script globs one or more eval-file patterns (relative to
    the Vally project root) and emits the result as an Azure Pipelines matrix object on
    an output variable so a downstream stage can fan out one job per shard (Stage 1
    "detect" of the mock-vertical skeleton).

    Granularity is a dial (design Section 2, Lever A) controlled by -ShardBy:
      - file : one shard per eval file (finest; default).
      - area : one shard per `area` tag (coarser; collapses many files into a handful of
               jobs once job-startup overhead dominates). No pipeline edits needed to switch.

    Each shard leg exposes two variables to the matrix job:
      - shardName : a filesystem-safe identifier (used for per-shard result folders)
      - evalArgs  : the `-e <file>` argument string passed verbatim to `vally eval`
                    (one file in 'file' mode, every file of an area in 'area' mode)

.PARAMETER EvalRoot
    Path to the Vally project root that contains `.vally.yaml` and the `evals/` tree.

.PARAMETER Pattern
    One or more glob patterns (relative to EvalRoot) selecting the eval files to shard.
    Defaults to the hermetic "mock vertical": unit tools + mock workflow scenarios.

.PARAMETER ShardBy
    'file' (default) for one shard per file, or 'area' for one shard per `area` tag.

.PARAMETER OutputVariableName
    Name of the Azure Pipelines output variable to set with the matrix JSON.

.OUTPUTS
    The matrix object (as a hashtable) is returned and also written as a compressed JSON
    `##vso[task.setVariable]` log command when running under Azure Pipelines.

.EXAMPLE
    ./Split-EvalSuite.ps1 -EvalRoot . -ShardBy area
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EvalRoot,

    [Parameter()]
    [string[]]$Pattern = @(
        'evals/tools/*.eval.yaml',
        'evals/workflow-scenarios/mock/*.eval.yaml'
    ),

    # Granularity dial (design Section 2, Lever A). 'file' = one shard per eval
    # file (finest; good while the file count is small). 'area' = one shard per
    # `area` tag (coarser; collapses many files into a handful of jobs once
    # job-startup overhead dominates). Switching modes needs no pipeline edits.
    [Parameter()]
    [ValidateSet('file', 'area')]
    [string]$ShardBy = 'file',

    [Parameter()]
    [string]$OutputVariableName = 'matrix'
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

function Get-EvalFile {
    [CmdletBinding()]
    param(
        [string]$Root,
        [string[]]$Pattern
    )

    # De-dup across patterns: overlapping globs (e.g. a broad and a narrow
    # pattern that both match a file) would otherwise yield the same eval twice,
    # which in 'area' mode emits a duplicate `-e <file>` (running the eval twice
    # in one shard) and in 'file' mode collides on shard name.
    $seen = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)

    foreach ($glob in $Pattern) {
        $files = Get-ChildItem -Path (Join-Path $Root $glob) -File -ErrorAction SilentlyContinue |
            Sort-Object -Property FullName
        foreach ($file in $files) {
            if (-not $seen.Add($file.FullName)) { continue }
            # Path relative to Root, forward-slashed so it is portable into the
            # `vally eval -e` argument on Linux agents.
            [PSCustomObject]@{
                FullName = $file.FullName
                Relative = [System.IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
                Leaf     = $file.Name -replace '\.eval\.yaml$', ''
                Parent   = Split-Path -Path (Split-Path -Path $file.FullName -Parent) -Leaf
            }
        }
    }
}

function Get-EvalArea {
    # Reads the `area` tag from an eval YAML. Targeted regex (not a full YAML
    # parse) so the script has no module dependency on the build agent. The eval
    # files use a flat `tags:` block, e.g. `  area: github`. Returns $null when no
    # tag is present so the caller can fall back to the file's folder.
    [CmdletBinding()]
    param(
        [string]$Path
    )

    $content = Get-Content -LiteralPath $Path -Raw
    if ($content -match '(?m)^\s*area:\s*["'']?([A-Za-z0-9._-]+)') {
        return $Matches[1]
    }
    return $null
}

function Get-EvalMatrix {
    [CmdletBinding()]
    param(
        [string]$EvalRoot,
        [string[]]$Pattern,
        [string]$ShardBy
    )

    $root = (Resolve-Path -LiteralPath $EvalRoot).Path
    $files = @(Get-EvalFile -Root $root -Pattern $Pattern)
    $matrix = [ordered]@{}

    if ($files.Count -eq 0) {
        throw "No eval files matched any of: $($Pattern -join ', ') under '$root'."
    }

    if ($ShardBy -eq 'file') {
        # One shard per file. Shard name = parent-folder + filename so tools/ and
        # mock/ legs of the same name cannot collide.
        foreach ($file in $files) {
            $shardName = ("{0}_{1}" -f $file.Parent, $file.Leaf) -replace '[^A-Za-z0-9]', '_'
            if ($matrix.Contains($shardName)) {
                throw "Duplicate shard name '$shardName' (from '$($file.Relative)'). Shard names must be unique."
            }
            $matrix[$shardName] = [ordered]@{
                shardName = $shardName
                evalArgs  = "-e $($file.Relative)"
            }
        }
    }
    else {
        # One shard per `area` tag. Every hermetic file carrying that area is run
        # in the same job via repeated `-e` flags (keeps the live tier out, unlike
        # the `--suite <area>` form which globs evals/** including live).
        $byArea = [ordered]@{}
        foreach ($file in ($files | Sort-Object Relative)) {
            $area = Get-EvalArea -Path $file.FullName
            if (-not $area) {
                # No `area:` tag. Fall back to the eval's parent folder so the file
                # still groups with its neighbours instead of all untagged evals
                # piling into one bucket. Warn so the missing tag stays visible.
                $area = $file.Parent
                Write-Warning "No 'area' tag in '$($file.Relative)'; falling back to folder '$area'."
            }
            if (-not $byArea.Contains($area)) {
                $byArea[$area] = [System.Collections.Generic.List[string]]::new()
            }
            $byArea[$area].Add($file.Relative)
        }
        foreach ($area in ($byArea.Keys | Sort-Object)) {
            $shardName = "area_$area" -replace '[^A-Za-z0-9]', '_'
            $evalArgs = ($byArea[$area] | ForEach-Object { "-e $_" }) -join ' '
            $matrix[$shardName] = [ordered]@{
                shardName = $shardName
                evalArgs  = $evalArgs
            }
        }
    }

    return $matrix
}

$result = Get-EvalMatrix -EvalRoot $EvalRoot -Pattern $Pattern -ShardBy $ShardBy
$json = $result | ConvertTo-Json -Depth 5 -Compress

Write-Host "Discovered $($result.Count) shard(s) (ShardBy=$ShardBy):"
foreach ($key in $result.Keys) {
    Write-Host "  - $key -> $($result[$key].evalArgs)"
}

# Emit for Azure Pipelines (no-op when run locally outside a pipeline).
Write-Host "##vso[task.setVariable variable=$OutputVariableName;isOutput=true]$json"

return $result
