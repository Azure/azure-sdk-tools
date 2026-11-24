Import-Module Pester

Set-StrictMode -Version 3

BeforeAll {
  . (Join-Path $PSScriptRoot '../ServiceApiReadiness-Helpers.ps1')

  function New-ReadinessResult([string]$Status = 'Passed', [string]$Plane = 'management-plane', [object[]]$MatchedTypes = $null) {
    if ($null -eq $MatchedTypes) {
      $MatchedTypes = @([pscustomobject]@{ resourceType = 'accounts'; regions = @('East US', 'West US') })
    }
    return [pscustomobject]@{
      status = $Status
      plane = $Plane
      headSha = 'a' * 40
      checkedAt = '2026-11-24T23:42:54+00:00'
      detail = 'API versions were read from SDK metadata files.'
      projects = @([pscustomobject]@{
          status = $Status
          namespace = 'Microsoft.CognitiveServices'
          detail = '1 of 2 provider resource types advertise the exact version.'
          source = [pscustomobject]@{
            packagePath = 'sdk/cognitiveservices/Azure.ResourceManager.CognitiveServices'
            commitSha = 'b' * 40
            apiVersion = '2026-11-15-preview'
            metadataFiles = @('tsp-location.yaml', 'metadata.json')
          }
          matches = $MatchedTypes
        })
    }
  }
}

Describe 'Get-SdkPullRequest' -Tag 'UnitTest' {
  It 'parses an Azure SDK pull request URL' {
    $pullRequest = Get-SdkPullRequest 'https://github.com/Azure/azure-sdk-for-go/pull/42'
    $pullRequest.Repository | Should -Be 'Azure/azure-sdk-for-go'
    $pullRequest.Number | Should -Be 42
  }

  It 'rejects <Url>' -ForEach @(
    @{ Url = 'https://github.com/Azure/azure-rest-api-specs/pull/1' }
    @{ Url = 'https://github.com/contoso/azure-sdk-for-net/pull/1' }
    @{ Url = 'https://github.com/Azure/azure-sdk-for-net/pull/1; rm -rf /' }
    @{ Url = 'https://github.com/Azure/azure-sdk-for-net/issues/1' }
  ) {
    { Get-SdkPullRequest $Url } | Should -Throw '*Azure SDK pull request URL*'
  }
}

Describe 'Test-ServiceApiReadinessPassed' -Tag 'UnitTest' {
  It 'returns <Expected> for <Status> <Plane>' -ForEach @(
    @{ Status = 'Passed'; Plane = 'management-plane'; Expected = $true }
    @{ Status = 'Passed'; Plane = 'data-plane'; Expected = $false }
    @{ Status = 'Not detected'; Plane = 'management-plane'; Expected = $false }
    @{ Status = 'Inconclusive'; Plane = 'management-plane'; Expected = $false }
  ) {
    Test-ServiceApiReadinessPassed ([pscustomobject]@{ status = $Status; plane = $Plane }) | Should -Be $Expected
  }
}

Describe 'Format-ServiceApiReadinessComment' -Tag 'UnitTest' {
  It 'starts with the shared marker and checked head' {
    $body = Format-ServiceApiReadinessComment (New-ReadinessResult) 'https://dev.azure.com/run/1'
    $body | Should -BeLike "$ServiceApiReadinessCommentMarker`n<!-- checked-head:$('a' * 40) -->`n*"
  }

  It 'shows <Display> for <Status> <Plane>' -ForEach @(
    @{ Status = 'Passed'; Plane = 'management-plane'; Display = 'Ready' }
    @{ Status = 'Not detected'; Plane = 'management-plane'; Display = 'Not ready' }
    @{ Status = 'Inconclusive'; Plane = 'management-plane'; Display = 'Unknown' }
    @{ Status = 'Inconclusive'; Plane = 'data-plane'; Display = 'Not applicable' }
  ) {
    $body = Format-ServiceApiReadinessComment (New-ReadinessResult $Status $Plane) 'https://dev.azure.com/run/1'
    $body | Should -Match "(?m)^## Service API-version check: $Display$"
    $body | Should -Match "(?m)^- Result: \*\*$Display\*\*$"
  }

  It 'lists matching resource types with regions and links the pipeline run' {
    $body = Format-ServiceApiReadinessComment (New-ReadinessResult) 'https://dev.azure.com/run/1'
    $body | Should -Match '\| accounts \| East US, West US \|'
    $body | Should -Match '<details><summary>Matching resource types \(1\)</summary>'
    $body | Should -Match '\[Pipeline run\]\(https://dev.azure.com/run/1\)'
  }

  It 'omits the resource-type section when nothing matched' {
    $body = Format-ServiceApiReadinessComment (New-ReadinessResult 'Not detected' -MatchedTypes @()) 'https://dev.azure.com/run/1'
    $body | Should -Not -Match '<details>'
  }

  It 'escapes markdown and HTML in CLI text' {
    $result = New-ReadinessResult
    $result.projects[0].detail = "<script>`n*bold* | pipe"
    $body = Format-ServiceApiReadinessComment $result 'https://dev.azure.com/run/1'
    $body | Should -Match ([regex]::Escape('- \<script\> \*bold\* \| pipe'))
  }

  It 'drops regions when the comment would exceed the GitHub limit' {
    $regions = @(1..60 | ForEach-Object { "Region $_ with a long name" })
    $matched = @(1..100 | ForEach-Object { [pscustomobject]@{ resourceType = "type$_"; regions = $regions } })
    $body = Format-ServiceApiReadinessComment (New-ReadinessResult -MatchedTypes $matched) 'https://dev.azure.com/run/1'

    $body.Length | Should -BeLessOrEqual $GitHubCommentLimit
    $body | Should -Not -Match 'Region 1 with'
    $body | Should -Match '(?m)^- type100$'
    $body | Should -Match 'Regions are omitted'
  }
}

Describe 'Format-ServiceApiReadinessErrorComment' -Tag 'UnitTest' {
  It 'reports Unknown for the checked head and links the run' {
    $body = Format-ServiceApiReadinessErrorComment ('c' * 40) 'https://dev.azure.com/run/2'
    $body | Should -BeLike "$ServiceApiReadinessCommentMarker`n<!-- checked-head:$('c' * 40) -->`n*"
    $body | Should -Match '(?m)^## Service API-version check: Unknown$'
    $body | Should -Match '\[Pipeline run\]\(https://dev.azure.com/run/2\)'
  }
}
