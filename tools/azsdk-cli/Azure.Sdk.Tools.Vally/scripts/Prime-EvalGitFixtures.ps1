#Requires -Version 7.0

<#
.SYNOPSIS
    Discovers the git worktree fixtures declared by the Vally eval suite and
    primes a local cache clone for each unique (repo, ref) it finds.

.DESCRIPTION
    Vally's `environment.git` fixtures do `git worktree add` from a repo-relative
    cache path; Vally does NOT clone that source itself (the worktree command
    needs it on disk). Rather than hard-code "clone azure-rest-api-specs", this
    script scans the eval YAMLs for every `git:` fixture block, resolves each
    `source:` to a concrete cache path, and ensures that repo is cloned at the
    requested `ref:` — so adding a new fixture repo needs no pipeline edits.

    Cloning is delegated to ensure-specs-clone.ps1 (shallow + blobless, with an
    optional cone-sparse checkout). The repo's clone URL is resolved by
    convention (https://github.com/<DefaultOrg>/<dir-name>.git), with per-repo
    overrides for URL and sparse paths in $KnownRepos below.

    If the scanned suite declares no git fixtures, this is a no-op (unit-only
    shards do not need a cache), so it is safe to run on every shard.

.PARAMETER EvalRoot
    Path to the Vally project root that contains the `evals/` tree.

.PARAMETER Pattern
    Glob patterns (relative to EvalRoot) selecting which eval files to scan.
    Defaults to the hermetic "mock vertical" suites the CI fans out over.

.PARAMETER MaxAgeHours
    Forwarded to ensure-specs-clone.ps1: skip the refresh fetch if the cache was
    primed within this many hours. Default: 24.

.PARAMETER DefaultOrg
    GitHub org used to build a clone URL when a repo is not in $KnownRepos.
    Default: Azure.

.EXAMPLE
    ./scripts/Prime-EvalGitFixtures.ps1 -EvalRoot .
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]   $EvalRoot,

    [Parameter()]
    [string[]] $Pattern = @(
        'evals/tools/*.eval.yaml',
        'evals/workflow-scenarios/mock/*.eval.yaml'
    ),

    [Parameter()]
    [int]      $MaxAgeHours = 24,

    [Parameter()]
    [string]   $DefaultOrg = 'Azure',

    # Dry-run: scan and emit the unique fixtures that WOULD be primed, then stop
    # before any clone. Useful for debugging and unit tests (no network).
    [Parameter()]
    [switch]   $ListOnly
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

# Per-repo overrides. Anything not listed falls back to the convention URL
# (https://github.com/<DefaultOrg>/<name>.git) and a full (non-sparse) checkout.
$KnownRepos = @{
    'azure-rest-api-specs' = @{
        Url    = 'https://github.com/Azure/azure-rest-api-specs.git'
        Sparse = @('specification/contosowidgetmanager')
    }
}

function Get-EvalGitFixture {
    <#
        Scans eval YAML files for `environment.git` worktree fixtures and emits
        one record per fixture: the declaring file, the raw source/ref, and the
        resolved absolute cache path + repo (leaf) name. Uses a targeted,
        indent-scoped regex rather than a YAML parser so there is no module
        dependency on the build agent (same approach as Get-EvalMatrix.ps1).
    #>
    [CmdletBinding()]
    param(
        [string]   $Root,
        [string[]] $Pattern
    )

    # Match a `git:` mapping and the more-indented block beneath it.
    $blockRegex = [regex]'(?m)^(?<indent>[ \t]*)git:[ \t]*\r?\n(?<body>(?:\k<indent>[ \t]+\S.*(?:\r?\n|$))+)'

    foreach ($glob in $Pattern) {
        $files = Get-ChildItem -Path (Join-Path $Root $glob) -File -ErrorAction SilentlyContinue |
            Sort-Object -Property FullName
        foreach ($file in $files) {
            $content = Get-Content -LiteralPath $file.FullName -Raw
            foreach ($m in $blockRegex.Matches($content)) {
                $body = $m.Groups['body'].Value

                $source = $null
                if ($body -match '(?m)^\s*source:\s*(?<v>\S+)') { $source = $Matches['v'] }
                if (-not $source) { continue }   # not a worktree/source fixture

                $ref = 'main'
                if ($body -match '(?m)^\s*ref:\s*(?<v>\S+)') { $ref = $Matches['v'] }

                # Resolve the repo-relative source against the eval file's folder.
                # GetFullPath (not Resolve-Path) so it works before the cache exists.
                $absPath = [System.IO.Path]::GetFullPath(
                    (Join-Path $file.DirectoryName $source))

                [PSCustomObject]@{
                    EvalFile  = $file.FullName
                    Source    = $source
                    Ref       = $ref
                    CachePath = $absPath
                    RepoName  = Split-Path -Path $absPath -Leaf
                }
            }
        }
    }
}

$root = (Resolve-Path -LiteralPath $EvalRoot).Path
$fixtures = @(Get-EvalGitFixture -Root $root -Pattern $Pattern)

if ($fixtures.Count -eq 0) {
    Write-Host "[prime-fixtures] No git fixtures declared by the scanned suite; nothing to prime."
    return
}

# One clone per unique (cache path, ref). The same repo pinned at two refs gets
# primed twice (different worktree sources); identical declarations collapse.
$unique = $fixtures | Sort-Object CachePath, Ref -Unique

Write-Host "[prime-fixtures] Discovered $($unique.Count) unique git fixture(s):"
foreach ($f in $unique) {
    Write-Host "  - $($f.RepoName) @ $($f.Ref)  ->  $($f.CachePath)"
}

if ($ListOnly) {
    # Dry-run: hand the discovered fixtures back to the caller, clone nothing.
    return $unique
}

$ensureScript = Join-Path $PSScriptRoot 'ensure-specs-clone.ps1'

foreach ($f in $unique) {
    $known     = $KnownRepos[$f.RepoName]
    $repoUrl   = if ($known) { $known.Url }    else { "https://github.com/$DefaultOrg/$($f.RepoName).git" }
    $sparse    = if ($known) { $known.Sparse } else { @() }
    $cacheRoot = Split-Path -Path $f.CachePath -Parent

    Write-Host "[prime-fixtures] Priming $($f.RepoName) @ $($f.Ref) from $repoUrl"
    & $ensureScript `
        -CacheRoot           $cacheRoot `
        -RepoUrl             $repoUrl `
        -RepoName            $f.RepoName `
        -Ref                 $f.Ref `
        -SparseCheckoutPaths $sparse `
        -MaxAgeHours         $MaxAgeHours | Out-Null
}

Write-Host "[prime-fixtures] Done."
