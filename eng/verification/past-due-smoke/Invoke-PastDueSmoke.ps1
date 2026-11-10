[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('BuildAndRender', 'DryRun')]
    [string]$Phase,

    [Parameter(Mandatory)]
    [string]$OutputDirectory
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$featureCommit = '392a8bc48f018284d3468c004c9ed04b3f9627c5'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

if (-not [string]::IsNullOrWhiteSpace($env:AZSDKTOOLS_NOTIFICATION_SERVICE_URL))
{
    throw 'The notification service URL must be empty for the smoke test.'
}

Push-Location $repoRoot
try
{
    $sourceCommit = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0)
    {
        throw 'Cannot determine the checked-out source commit.'
    }
    git diff --exit-code $featureCommit -- tools/azsdk-cli
    if ($LASTEXITCODE -ne 0)
    {
        throw 'CLI sources differ from the reviewed PR commit.'
    }

    $cliPath = Join-Path $repoRoot 'artifacts/bin/Azure.Sdk.Tools.Cli/Release/net8.0/azsdk.dll'
    $projectPath = Join-Path $PSScriptRoot 'PastDueSmoke.csproj'
    if ($Phase -eq 'BuildAndRender')
    {
        dotnet build $projectPath --configuration Release -p:CopilotSkipCliDownload=true --verbosity minimal
        if ($LASTEXITCODE -ne 0)
        {
            throw 'PR CLI and reminder renderer build failed.'
        }
        $version = dotnet $cliPath --version
        if ($LASTEXITCODE -ne 0 -or $version -notmatch '^0\.6\.58')
        {
            throw 'The built CLI version does not match the reviewed PR.'
        }
        dotnet run --project $projectPath --configuration Release --no-build -- $OutputDirectory
        if ($LASTEXITCODE -ne 0)
        {
            throw 'Synthetic reminder rendering failed.'
        }
        $identity = [ordered]@{
            sourceCommit = $sourceCommit
            reviewedFeatureCommit = $featureCommit
            cliVersion = $version
            cliSha256 = (Get-FileHash -LiteralPath $cliPath -Algorithm SHA256).Hash
            mutatingCommandsEnabled = $false
            emailSendingEnabled = $false
        }
        [IO.File]::WriteAllText((Join-Path $OutputDirectory 'source-identity.json'), ($identity | ConvertTo-Json))
        return
    }

    if (-not (Test-Path -LiteralPath $cliPath))
    {
        throw 'The PR-built CLI was not found.'
    }
    $stdoutPath = Join-Path $OutputDirectory 'cleanup-dry-run.json'
    $stderrPath = Join-Path $OutputDirectory 'cleanup-dry-run.stderr.log'
    dotnet $cliPath release-plan abandon-overdue --dry-run --inactivity-days 30 --output json 1> $stdoutPath 2> $stderrPath
    $cliExitCode = $LASTEXITCODE
    $response = [IO.File]::ReadAllText($stdoutPath) | ConvertFrom-Json
    if ($response.dry_run -ne $true -or $null -eq $response.preview_summary)
    {
        throw 'The CLI did not return a read-only cleanup preview.'
    }
    $summary = [ordered]@{
        sourceCommit = $sourceCommit
        reviewedFeatureCommit = $featureCommit
        cliExitCode = $cliExitCode
        dryRun = $response.dry_run
        preview = $response.preview_summary
        mutatingCommandsEnabled = $false
        emailSendingEnabled = $false
    }
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'smoke-summary.json'), ($summary | ConvertTo-Json -Depth 10))
    $counts = $response.preview_summary
    Write-Output "Cleanup preview: scanned=$($counts.scanned), eligible=$($counts.eligible), skipped=$($counts.skipped), evaluationErrors=$($counts.evaluation_errors)."
    if ($cliExitCode -ne 0 -or $counts.evaluation_errors -ne 0)
    {
        throw 'Read-only cleanup preview reported errors; inspect the private evidence artifact.'
    }
}
finally
{
    Pop-Location
}