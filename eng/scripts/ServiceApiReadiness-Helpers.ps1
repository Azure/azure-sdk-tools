# Formatting helpers for Invoke-ServiceApiReadinessCheck.ps1. The comment marker
# and label names match the azure-sdk-automation readiness action so both
# writers update the same PR comment and label.

Set-StrictMode -Version 4

$ServiceApiReadinessPassedLabel = 'service-api-version-check-passed'
$ServiceApiReadinessCommentMarker = '<!-- azsdk-service-api-version-check:v1 -->'

# GitHub rejects issue comments longer than 65,536 characters.
$GitHubCommentLimit = 65536

function Get-SdkPullRequest([string]$PullRequestUrl) {
  if ($PullRequestUrl -cnotmatch '^https://github\.com/Azure/(azure-sdk-for-(?:net|java|js|python|go))/pull/([1-9]\d*)$') {
    throw "PullRequestUrl must be an Azure SDK pull request URL, such as https://github.com/Azure/azure-sdk-for-net/pull/123."
  }
  return [pscustomobject]@{
    Repository = "Azure/$($Matches[1])"
    Number = [int]$Matches[2]
  }
}

function Test-ServiceApiReadinessPassed($Result) {
  return $Result.status -eq 'Passed' -and $Result.plane -eq 'management-plane'
}

function Get-ReadinessDisplayStatus([string]$Status, [string]$Plane) {
  if ($Plane -eq 'data-plane') { return 'Not applicable' }
  switch ($Status) {
    'Passed' { return 'Ready' }
    'Not detected' { return 'Not ready' }
    default { return 'Unknown' }
  }
}

function ConvertTo-MarkdownText([string]$Value) {
  return ($Value -replace '[\r\n]', ' ') -replace '([\\`*_\[\]<>|])', '\$1'
}

function Format-CommentHeader([string]$HeadSha, [string]$DisplayStatus) {
  return @(
    $ServiceApiReadinessCommentMarker
    "<!-- checked-head:$HeadSha -->"
    "## Service API-version check: $DisplayStatus"
    ''
  )
}

function Format-ServiceApiReadinessComment($Result, [string]$PipelineRunUrl) {
  $lines = Format-ServiceApiReadinessCommentLines $Result $PipelineRunUrl -IncludeRegions
  $body = $lines -join "`n"
  if ($body.Length -le $GitHubCommentLimit) { return $body }

  $lines = Format-ServiceApiReadinessCommentLines $Result $PipelineRunUrl
  $lines += @('', "Regions are omitted because the full result is longer than a GitHub comment allows. See the [pipeline run]($PipelineRunUrl) for the full result.")
  return $lines -join "`n"
}

function Format-ServiceApiReadinessCommentLines($Result, [string]$PipelineRunUrl, [switch]$IncludeRegions) {
  $lines = Format-CommentHeader $Result.headSha (Get-ReadinessDisplayStatus $Result.status $Result.plane)
  $lines += @(
    "PR head: ``$($Result.headSha)`` | Checked: $($Result.checkedAt) | Cloud: Azure public | [Pipeline run]($PipelineRunUrl)"
    ''
    (ConvertTo-MarkdownText $Result.detail)
    ''
    'This is metadata evidence, not release approval, service health, or proof of deployment in every region.'
  )

  foreach ($project in $Result.projects) {
    $source = $project.source
    $lines += @(
      ''
      "### $(ConvertTo-MarkdownText $source.packagePath)"
      "- Result: **$(Get-ReadinessDisplayStatus $project.status $Result.plane)**"
      "- Spec commit: $(ConvertTo-MarkdownText ($source.commitSha ? $source.commitSha : 'missing'))"
      "- API version: $(ConvertTo-MarkdownText ($source.apiVersion ? $source.apiVersion : 'missing'))"
      "- Provider: $(ConvertTo-MarkdownText ($project.namespace ? $project.namespace : 'unresolved'))"
      "- SDK metadata files: $(ConvertTo-MarkdownText ($source.metadataFiles -join ', '))"
      "- $(ConvertTo-MarkdownText $project.detail)"
    )

    $matchedTypes = @($project.matches)
    if ($matchedTypes.Count -eq 0) { continue }
    $lines += @('', "<details><summary>Matching resource types ($($matchedTypes.Count))</summary>", '')
    if ($IncludeRegions) {
      $lines += @('| Matching resource type | Advertised regions |', '| --- | --- |')
      foreach ($match in $matchedTypes) {
        $regions = @($match.regions) -join ', '
        $lines += "| $(ConvertTo-MarkdownText $match.resourceType) | $(ConvertTo-MarkdownText ($regions ? $regions : 'Not listed')) |"
      }
    }
    else {
      $lines += @($matchedTypes | ForEach-Object { "- $(ConvertTo-MarkdownText $_.resourceType)" })
    }
    $lines += @('', '</details>')
  }

  return $lines
}

function Format-ServiceApiReadinessErrorComment([string]$HeadSha, [string]$PipelineRunUrl) {
  $lines = Format-CommentHeader $HeadSha 'Unknown'
  $lines += @(
    "PR head: ``$HeadSha`` | [Pipeline run]($PipelineRunUrl)"
    ''
    'The azsdk readiness command did not complete. See the pipeline run for details, then remove and re-add `service-api-version-check-requested` to retry.'
  )
  return $lines -join "`n"
}
