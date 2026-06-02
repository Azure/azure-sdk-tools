<#
.SYNOPSIS
  Runs Vally live-tier evals locally with shared spec-repo setup.

.DESCRIPTION
  - Calls evals/setup/ensure-specs-clone.ps1 once to prime the azure-rest-api-specs
    cache (idempotent, refreshes if >24h old).
  - Then runs the given eval spec(s) via the locally-installed Vally CLI.

  Defaults to the release-planner-e2e demo. Pass -EvalSpecs to run others.

.EXAMPLE
  ./Run-LiveEvals.ps1

.EXAMPLE
  ./Run-LiveEvals.ps1 -EvalSpecs evals/e2e/release-planner-e2e.eval.yaml,evals/e2e/foo.eval.yaml
#>
[CmdletBinding()]
param(
    [string[]] $EvalSpecs = @('evals/e2e/release-planner-e2e.eval.yaml'),
    [switch]   $VallyVerbose
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 4

$repoRoot   = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$vallyCli   = Join-Path $repoRoot 'eng\skill-eval\node_modules\.bin\vally.cmd'
$setupScript = Join-Path $PSScriptRoot 'evals\setup\ensure-specs-clone.ps1'

if (-not (Test-Path $vallyCli)) {
    throw "Vally CLI not found at $vallyCli. Run 'npm install' in eng/skill-eval first."
}

Write-Host "==> Ensuring azure-rest-api-specs cache"
& pwsh -NoProfile -File $setupScript | Out-Host

Push-Location $PSScriptRoot
try {
    foreach ($spec in $EvalSpecs) {
        Write-Host ""
        Write-Host "==> Running $spec"
        $args = @('eval', '--eval-spec', $spec)
        if ($VallyVerbose) { $args += '--verbose' }
        & $vallyCli @args
        if ($LASTEXITCODE -ne 0) {
            throw "Eval failed: $spec (exit $LASTEXITCODE)"
        }
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "==> All evals passed."
