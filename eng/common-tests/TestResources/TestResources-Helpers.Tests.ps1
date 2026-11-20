# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

BeforeAll {
    $helperPath = Join-Path $PSScriptRoot '../../common/TestResources/TestResources-Helpers.ps1'
    . $helperPath

    function Get-AzResourceGroupDeployment {
        [CmdletBinding()]
        param([string] $ResourceGroupName, [string] $Name)
        throw 'Unexpected Azure deployment lookup.'
    }

    function New-AzResourceGroupDeployment {
        [CmdletBinding()]
        param(
            [string] $Name,
            [string] $ResourceGroupName,
            [string] $TemplateFile,
            [hashtable] $TemplateParameterObject,
            [switch] $Force
        )
        throw 'Unexpected Azure deployment.'
    }
}

Describe 'Deployment failure reporting' -Tag 'UnitTest' {
    It 'throws for <State> even when its output is discarded' -ForEach @(
        @{ State = 'Failed' }
        @{ State = 'Canceled' }
    ) {
        $deployment = [pscustomobject]@{
            DeploymentName = 'test-deployment'
            ProvisioningState = $State
            CorrelationId = 'test-correlation'
        }

        { HandleDeploymentFailure $deployment | Out-Null } |
            Should -Throw "*test-deployment*${State}*test-correlation*"
    }

    It 'preserves the original ARM error' {
        $deployment = [pscustomobject]@{
            DeploymentName = 'test-deployment'
            ProvisioningState = 'Failed'
            CorrelationId = 'test-correlation'
        }
        $deploymentError = try {
            throw 'Account Kind BlobStorage is not supported. (Code:AccountKindNotSupported)'
        } catch {
            $_
        }

        { HandleDeploymentFailure $deployment -DeploymentError $deploymentError | Out-Null } |
            Should -Throw '*AccountKindNotSupported*'
    }

    It 'stops a calling PowerShell process before tests can start' {
        $command = @"
`$ErrorActionPreference = 'Stop'
. '$helperPath'
HandleDeploymentFailure ([pscustomobject]@{
    DeploymentName = 'test-deployment'
    ProvisioningState = 'Failed'
    CorrelationId = 'test-correlation'
}) | Out-Null
Write-Output 'TESTS_STARTED'
"@
        $pwsh = (Get-Process -Id $PID).Path
        $output = & $pwsh -NoProfile -NonInteractive -Command $command 2>&1
        $LASTEXITCODE | Should -Not -Be 0
        ($output | Out-String) | Should -Not -Match 'TESTS_STARTED'
    }
}

Describe 'Deployment recovery' -Tag 'UnitTest' {
    BeforeEach {
        $parameters = @{
            DeploymentName = 'test-deployment'
            ResourceGroupName = 'test-resource-group'
            TemplateFile = 'test-resources.json'
            TemplateFileParameters = @{}
        }
        Mock New-AzResourceGroupDeployment { throw 'A terminal deployment must not be retried.' }
        Mock Start-Sleep {}
    }

    It 'returns an existing <State> deployment without resubmitting it' -ForEach @(
        @{ State = 'Succeeded' }
        @{ State = 'Failed' }
        @{ State = 'Canceled' }
    ) {
        $script:deployment = [pscustomobject]@{
            DeploymentName = 'test-deployment'
            ProvisioningState = $State
            CorrelationId = 'test-correlation'
            Outputs = @{ TEST_OUTPUT = @{ Value = 'test-value' } }
        }
        Mock Get-AzResourceGroupDeployment { $script:deployment }

        $result = Resolve-DeploymentAfterFailure @parameters
        $result | Should -Be $script:deployment
        $result.Outputs.TEST_OUTPUT.Value | Should -Be 'test-value'
        Should -Invoke New-AzResourceGroupDeployment -Times 0 -Exactly
    }

    It 'waits for an active deployment without starting another one' {
        $script:lookupCount = 0
        Mock Get-AzResourceGroupDeployment {
            $script:lookupCount++
            [pscustomobject]@{
                ProvisioningState = if ($script:lookupCount -lt 3) { 'Running' } else { 'Succeeded' }
            }
        }

        $result = Resolve-DeploymentAfterFailure @parameters
        $result.ProvisioningState | Should -Be 'Succeeded'
        Should -Invoke Start-Sleep -Times 1 -Exactly
        Should -Invoke New-AzResourceGroupDeployment -Times 0 -Exactly
    }
}
