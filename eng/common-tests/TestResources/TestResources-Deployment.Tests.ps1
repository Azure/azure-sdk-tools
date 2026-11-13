# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

BeforeAll {
    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..')).Path
    $script:HelpersPath = Join-Path $repositoryRoot 'eng' 'common' 'TestResources' 'TestResources-Helpers.ps1'
    $deploymentScript = Join-Path $repositoryRoot 'eng' 'common' 'TestResources' 'New-TestResources.ps1'
    $script:PowerShellPath = (Get-Command pwsh -CommandType Application).Source

    # Exercise the actual failure gate without loading Az modules or provisioning resources.
    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile(
        $deploymentScript, [ref] $tokens, [ref] $parseErrors
    )
    if ($parseErrors) {
        throw "Could not parse New-TestResources.ps1: $parseErrors"
    }
    $failureGate = $ast.Find({
        param($node)
        return $node -is [System.Management.Automation.Language.IfStatementAst] -and
            $node.Extent.Text -match '\bHandleDeploymentFailure\b'
    }, $true)
    if (!$failureGate) {
        throw 'Could not find the deployment failure gate in New-TestResources.ps1.'
    }
    $script:FailureGate = $failureGate.Extent.Text
}

Describe 'Test-resource deployment task result' -Tag 'UnitTest' {
    It 'propagates a <State> ARM deployment to the task result' -ForEach @(
        @{ State = 'Failed'; ExpectedExitCode = 1 }
        @{ State = 'Canceled'; ExpectedExitCode = 1 }
        @{ State = 'Succeeded'; ExpectedExitCode = 0 }
    ) {
        $deploymentPath = Join-Path $TestDrive "deployment-$State.ps1"
        $taskPath = Join-Path $TestDrive "task-$State.ps1"
        $postPath = Join-Path $TestDrive "post-$State.ps1"
        $helpersLiteral = $script:HelpersPath.Replace("'", "''")
        $deploymentLiteral = $deploymentPath.Replace("'", "''")
        $taskLiteral = $taskPath.Replace("'", "''")
        $postLiteral = $postPath.Replace("'", "''")

        @"
. '$helpersLiteral'
`$deployment = [pscustomobject]@{
    DeploymentName = 'test-deployment'
    ProvisioningState = '$State'
    CorrelationId = 'test-correlation'
}
try {
$script:FailureGate
    New-Item -Path '$postLiteral' -ItemType File | Out-Null
} finally {
    Write-Host 'Deployment cleanup completed'
}
"@ | Set-Content -LiteralPath $deploymentPath

        @"
`$ErrorActionPreference = 'Stop'
& '$deploymentLiteral' | Out-Null
Write-Host 'Task continued after deployment'
"@ | Set-Content -LiteralPath $taskPath

        $PSNativeCommandUseErrorActionPreference = $false
        $output = & $script:PowerShellPath -NoLogo -NoProfile -NonInteractive `
            -ExecutionPolicy Unrestricted -Command ". '$taskLiteral'" 2>&1
        $exitCode = $LASTEXITCODE
        $outputText = ($output | ForEach-Object { $_.ToString() }) -join "`n"

        $exitCode | Should -Be $ExpectedExitCode
        $outputText | Should -Match 'Deployment cleanup completed'
        if ($ExpectedExitCode -eq 0) {
            $postPath | Should -Exist
            $outputText | Should -Match 'Task continued after deployment'
        } else {
            $postPath | Should -Not -Exist
            $outputText | Should -Not -Match 'Task continued after deployment'
            $outputText | Should -Match "Deployment 'test-deployment' has state '$State'"
            $outputText | Should -Match 'test-correlation'
        }
    }
}
