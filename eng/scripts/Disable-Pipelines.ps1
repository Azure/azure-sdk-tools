<#
.SYNOPSIS
Disables a set of Azure DevOps pipelines (build definitions) by setting their queue
status to 'disabled' via the Build Definitions REST API.

.DESCRIPTION
Takes a comma (or whitespace) separated list of pipeline definition IDs and disables each
one so that no new runs can be queued (manually or by triggers). For each definition the
script fetches the full definition, sets its 'queueStatus' to 'disabled', and PUTs the
updated definition back. A failure to disable a single pipeline is reported but does not
stop the remaining pipelines. The script prints a summary and exits with a non-zero code
if any pipeline could not be disabled.

Authentication uses either a Bearer access token (recommended, obtained from the
'opensource-api-connection' service connection) or a PAT-derived Base64 encoded token.

.PARAMETER PipelineIds
Comma (or whitespace) separated list of pipeline definition IDs to disable. Required.

.PARAMETER Organization
Azure DevOps organization name. Defaults to 'azure-sdk'.

.PARAMETER Project
Azure DevOps project name. Defaults to 'internal'.

.PARAMETER BearerToken
Azure DevOps access token obtained via:
  az account get-access-token --resource "499b84ac-1321-427f-aa17-267ca6975798" --query "accessToken" --output tsv

.PARAMETER Base64EncodedToken
Base64 encoded PAT (Basic auth) as an alternative to BearerToken.

.EXAMPLE
./Disable-Pipelines.ps1 -PipelineIds "1234,5678" -BearerToken $token

.EXAMPLE
./Disable-Pipelines.ps1 -PipelineIds "1234 5678" -Base64EncodedToken $encoded -WhatIf
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [Parameter(Mandatory = $true)]
  [string] $PipelineIds,

  [string] $Organization = "azure-sdk",

  [string] $Project = "internal",

  [string] $BearerToken,

  [string] $Base64EncodedToken
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

# Import shared logic unless a caller (e.g. a test harness) has already provided it.
if (-not (Get-Command 'LogInfo' -ErrorAction SilentlyContinue)) {
  . (Join-Path $PSScriptRoot ".." "common" "scripts" "logging.ps1")
}
if (-not (Get-Command 'Get-DevOpsApiHeaders' -ErrorAction SilentlyContinue)) {
  . (Join-Path $PSScriptRoot ".." "common" "scripts" "Invoke-DevOpsAPI.ps1")
}

# Safe property accessor: under Set-StrictMode referencing a non-existent property throws,
# so guard reads of the externally-provided definition object.
function Get-Prop($obj, [string]$name, $default = $null) {
  if ($null -ne $obj -and ($obj.PSObject.Properties.Name -contains $name)) {
    return $obj.$name
  }
  return $default
}

# Fetch a single build definition. Defined inline (guarded) so a test harness can stub it.
if (-not (Get-Command 'Get-DevOpsBuildDefinition' -ErrorAction SilentlyContinue)) {
  function Get-DevOpsBuildDefinition {
    param(
      $Organization = 'azure-sdk',
      $Project = 'internal',
      [Parameter(Mandatory = $true)] $DefinitionId,
      $Base64EncodedToken = $null,
      $BearerToken = $null
    )
    $uri = $DevOpsAPIBaseURI -F $Organization, $Project, "build", "definitions/$DefinitionId", ""
    $headers = (Get-DevOpsApiHeaders -Base64EncodedToken $Base64EncodedToken -BearerToken $BearerToken)
    return Invoke-RestMethod -Method GET -Uri $uri -Headers $headers -MaximumRetryCount 3
  }
}

# Update (PUT) a build definition. Defined inline (guarded) so a test harness can stub it.
if (-not (Get-Command 'Set-DevOpsBuildDefinition' -ErrorAction SilentlyContinue)) {
  function Set-DevOpsBuildDefinition {
    param(
      $Organization = 'azure-sdk',
      $Project = 'internal',
      [Parameter(Mandatory = $true)] $DefinitionId,
      [Parameter(Mandatory = $true)] $Definition,
      $Base64EncodedToken = $null,
      $BearerToken = $null
    )
    $uri = $DevOpsAPIBaseURI -F $Organization, $Project, "build", "definitions/$DefinitionId", ""
    $headers = (Get-DevOpsApiHeaders -Base64EncodedToken $Base64EncodedToken -BearerToken $BearerToken)
    return Invoke-RestMethod -Method PUT -Uri $uri -Headers $headers -MaximumRetryCount 3 `
      -Body ($Definition | ConvertTo-Json -Depth 100) -ContentType "application/json"
  }
}

# --- Resolve the list of pipeline definition IDs to disable -----------------
$definitionIds = @()
foreach ($idStr in ($PipelineIds -split '[,\s]+' | Where-Object { $_ })) {
  $idVal = 0
  if (-not [int]::TryParse($idStr.Trim(), [ref]$idVal)) {
    LogWarning "Ignoring invalid pipeline id '$idStr' (not an integer)."
    continue
  }
  if ($idVal -le 0) {
    LogWarning "Ignoring non-positive pipeline id '$idStr'."
    continue
  }
  $definitionIds += $idVal
}

$definitionIds = @($definitionIds | Select-Object -Unique)

if ($definitionIds.Count -eq 0) {
  LogError "No valid pipeline definition IDs provided in -PipelineIds '$PipelineIds'."
  exit 1
}

# --- Disable each pipeline --------------------------------------------------
LogInfo "Disabling $($definitionIds.Count) pipeline(s) in $Organization/$Project."

$results = @()
foreach ($definitionId in $definitionIds) {
  $label = "definition $definitionId in $Organization/$Project"

  $definition = $null
  try {
    $definition = Get-DevOpsBuildDefinition -Organization $Organization -Project $Project `
      -DefinitionId $definitionId -BearerToken $BearerToken -Base64EncodedToken $Base64EncodedToken
  }
  catch {
    LogError "Failed to fetch definition $definitionId : $_"
    $results += [pscustomobject]@{ DefinitionId = $definitionId; Name = ''; Status = 'Fetch failed' }
    continue
  }

  $name = Get-Prop $definition 'name' "Definition $definitionId"
  $currentStatus = Get-Prop $definition 'queueStatus' 'unknown'

  if ($currentStatus -eq 'disabled') {
    LogInfo "[$name] (definition $definitionId) is already disabled. Skipping."
    $results += [pscustomobject]@{ DefinitionId = $definitionId; Name = $name; Status = 'Already disabled' }
    continue
  }

  if (-not $PSCmdlet.ShouldProcess($label, "Disable pipeline (set queueStatus=disabled)")) {
    $results += [pscustomobject]@{ DefinitionId = $definitionId; Name = $name; Status = 'Skipped (WhatIf)' }
    continue
  }

  # PUT requires the full definition object; only the queue status is changed.
  $definition.queueStatus = 'disabled'

  try {
    $updated = Set-DevOpsBuildDefinition -Organization $Organization -Project $Project `
      -DefinitionId $definitionId -Definition $definition `
      -BearerToken $BearerToken -Base64EncodedToken $Base64EncodedToken

    $newStatus = Get-Prop $updated 'queueStatus' 'unknown'
    if ($newStatus -eq 'disabled') {
      LogSuccess "Disabled [$name] (definition $definitionId)."
      $results += [pscustomobject]@{ DefinitionId = $definitionId; Name = $name; Status = 'Disabled' }
    }
    else {
      LogWarning "PUT for [$name] (definition $definitionId) returned queueStatus '$newStatus' (expected 'disabled')."
      $results += [pscustomobject]@{ DefinitionId = $definitionId; Name = $name; Status = "Unexpected status: $newStatus" }
    }
  }
  catch {
    LogError "Failed to disable definition $definitionId ([$name]): $_"
    $results += [pscustomobject]@{ DefinitionId = $definitionId; Name = $name; Status = 'Disable failed' }
  }
}

# --- Summary ----------------------------------------------------------------
LogInfo "Disable summary:"
($results | Format-Table DefinitionId, Name, Status -AutoSize | Out-String).TrimEnd() | Write-Host

# A pipeline counts as failed if it could not be fetched, could not be disabled, or ended
# in an unexpected queue status.
$failures = @($results | Where-Object {
    $_.Status -eq 'Fetch failed' -or
    $_.Status -eq 'Disable failed' -or
    $_.Status -like 'Unexpected status:*'
  })
$failureCount = $failures.Count
if ($failureCount -gt 0) {
  LogError "$failureCount pipeline(s) could not be disabled."
}

exit $failureCount
