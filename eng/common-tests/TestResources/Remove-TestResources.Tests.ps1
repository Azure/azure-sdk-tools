Import-Module Pester

Describe "Remove-TestResources directory cleanup" -Tag "UnitTest", "TestResources" {
    BeforeAll {
        $path = Join-Path $PSScriptRoot "../../common/TestResources/Remove-TestResources.ps1"
        $tokens = $null
        $parseErrors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseFile(
            (Resolve-Path $path), [ref]$tokens, [ref]$parseErrors)
        if ($parseErrors.Count) {
            throw "Could not parse Remove-TestResources.ps1: $parseErrors"
        }

        # Execute the script's filesystem cleanup block without loading Azure modules or deleting cloud resources.
        $blocks = @($ast.EndBlock.Statements | Where-Object {
            $_ -is [System.Management.Automation.Language.IfStatementAst] -and
            $_.Clauses[0].Item1.Extent.Text -eq '$ServiceDirectory'
        })
        if ($blocks.Count -ne 1) {
            throw "Expected one ServiceDirectory cleanup block, found $($blocks.Count)."
        }
        $script:cleanup = [scriptblock]::Create(@'
[CmdletBinding(DefaultParameterSetName = 'ResourceGroup')]
param($PSScriptRoot, $ServiceDirectory, $ResourceType = 'test', [switch]$Force, $ResourceGroupName = 'fixture-group')
$ErrorActionPreference = 'Stop'
function Log([string]$Message) {}
'@ + "`n" + $blocks[0].Extent.Text)
    }

    BeforeEach {
        $script:repository = Join-Path $TestDrive ([guid]::NewGuid().ToString())
        $script:scriptRoot = Join-Path $repository "eng/common/TestResources"
        New-Item -ItemType Directory -Path $scriptRoot -Force | Out-Null
        $script:service = Join-Path $repository "sdk/keyvault"
        New-Item -ItemType Directory -Path $service -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $service ".env") -Value "fixture"
    }

    It "cleans a service directory relative to sdk" {
        & $cleanup -PSScriptRoot $scriptRoot -ServiceDirectory "keyvault" -Force
        Test-Path -LiteralPath (Join-Path $service ".env") | Should -BeFalse
    }

    It "cleans a nested service directory without touching its parent" {
        $nested = Join-Path $service "nested"
        New-Item -ItemType Directory -Path $nested | Out-Null
        Set-Content -LiteralPath (Join-Path $nested ".env") -Value "fixture"

        & $cleanup -PSScriptRoot $scriptRoot -ServiceDirectory "keyvault/nested" -Force
        Test-Path -LiteralPath (Join-Path $nested ".env") | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $service ".env") | Should -BeTrue
    }

    It "cleans an absolute smoke-test directory, including spaces and a trailing separator" {
        $smoke = Join-Path $repository "common/smoke tests"
        $nested = Join-Path $smoke "nested"
        New-Item -ItemType Directory -Path $nested -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $nested ".env") -Value "fixture"
        Set-Content -LiteralPath (Join-Path $nested "test-resources.json.env") -Value "fixture"
        Set-Content -LiteralPath (Join-Path $nested "perf-resources.json.env") -Value "fixture"

        & $cleanup -PSScriptRoot $scriptRoot -ServiceDirectory "$smoke/" -Force
        Test-Path -LiteralPath (Join-Path $nested ".env") | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $nested "test-resources.json.env") | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $nested "perf-resources.json.env") | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $service ".env") | Should -BeTrue
    }

    It "preserves parent-relative directory support" {
        $smoke = Join-Path $repository "common/smoketest"
        New-Item -ItemType Directory -Path $smoke -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $smoke ".env") -Value "fixture"

        & $cleanup -PSScriptRoot $scriptRoot -ServiceDirectory "../common/smoketest" -Force
        Test-Path -LiteralPath (Join-Path $smoke ".env") | Should -BeFalse
    }

    It "invokes the pre-removal hook with the original resource group" {
        Set-Content -LiteralPath (Join-Path $service "remove-test-resources-pre.ps1") -Value @'
param([string]$ResourceGroupName, [Parameter(ValueFromRemainingArguments = $true)]$Remaining)
Set-Content -LiteralPath (Join-Path $PSScriptRoot "hook-result.txt") -Value $ResourceGroupName
'@
        & $cleanup -PSScriptRoot $scriptRoot -ServiceDirectory $service -ResourceGroupName "expected-group" -Force
        Get-Content -LiteralPath (Join-Path $service "hook-result.txt") | Should -Be "expected-group"
        Test-Path -LiteralPath (Join-Path $service ".env") | Should -BeFalse
    }

    It "does not clean files when ServiceDirectory is omitted" {
        & $cleanup -PSScriptRoot $scriptRoot -Force
        Test-Path -LiteralPath (Join-Path $service ".env") | Should -BeTrue
    }

    It "reports a missing directory without cleaning another service" {
        { & $cleanup -PSScriptRoot $scriptRoot -ServiceDirectory "missing" -Force } | Should -Throw
        Test-Path -LiteralPath (Join-Path $service ".env") | Should -BeTrue
    }
}
