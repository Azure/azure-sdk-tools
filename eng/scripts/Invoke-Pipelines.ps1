<#
.SYNOPSIS
Triggers (queues) a set of pre-configured Azure DevOps pipelines one at a time, waiting
for each run to finish before triggering the next.

.DESCRIPTION
Reads a list of pipeline definition IDs (from a JSON config file and/or a comma-separated
override) and triggers each one sequentially, because the configured pipelines cannot run
in parallel. For each pipeline it queues a run, polls the run status on an interval until
it completes, and only then triggers the next pipeline. A failure to queue or a
non-succeeded run result is reported but does not stop the remaining pipelines. The script
prints a summary of the runs and exits with a non-zero code if any pipeline did not
succeed.

Authentication uses either a Bearer access token (recommended, obtained from the
'opensource-api-connection' service connection) or a PAT-derived Base64 encoded token.

.PARAMETER ConfigPath
Path to the JSON config file describing the pipelines to trigger. Defaults to
eng/pipelines/trigger-pipelines-config.json.

.PARAMETER DefinitionIds
Optional comma (or whitespace) separated list of pipeline definition IDs. When provided,
this list overrides the pipeline list from the config file. Organization and project are
still taken from the parameters or the config file.

.PARAMETER Organization
Azure DevOps organization name. Overrides the value from the config file. Defaults to
'azure-sdk'.

.PARAMETER Project
Azure DevOps project name. Overrides the value from the config file. Defaults to
'internal'.

.PARAMETER SourceBranch
Source branch to run the pipelines against. An empty value (default) queues each pipeline
on its configured default branch.

.PARAMETER BearerToken
Azure DevOps access token obtained via:
  az account get-access-token --resource "499b84ac-1321-427f-aa17-267ca6975798" --query "accessToken" --output tsv

.PARAMETER Base64EncodedToken
Base64 encoded PAT (Basic auth) as an alternative to BearerToken.

.PARAMETER TemplateParametersJson
Optional JSON object of runtime template parameters applied to every triggered pipeline.

.PARAMETER BuildParametersJson
Optional JSON object of pipeline variables applied to every triggered pipeline.

.PARAMETER PollIntervalSeconds
How often, in seconds, to poll a queued run for completion before triggering the next
pipeline. Defaults to 300 (5 minutes).

.PARAMETER TokenResource
The Azure DevOps resource id used to refresh the bearer token via the az CLI during long
sequential runs. Defaults to the Azure DevOps resource ('499b84ac-1321-427f-aa17-267ca6975798').

.PARAMETER PollTimeoutMinutes
Safety cap, in minutes, for how long to wait for a single run to complete before giving up
on it and moving on. Defaults to 0 (no cap; relies on the consecutive-error guard instead).

.PARAMETER NoWait
Queue every pipeline back-to-back without waiting for any run to complete. Useful when the
pipelines can run in parallel. The exit code then reflects only queue failures.

.EXAMPLE
./Invoke-Pipelines.ps1 -BearerToken $token

.EXAMPLE
./Invoke-Pipelines.ps1 -DefinitionIds "1234,5678" -BearerToken $token
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [string] $ConfigPath = (Join-Path $PSScriptRoot ".." "pipelines" "trigger-pipelines-config.json"),

  [string] $DefinitionIds,

  [string] $Organization,

  [string] $Project,

  [string] $SourceBranch = "",

  [string] $BearerToken,

  [string] $Base64EncodedToken,

  [string] $TemplateParametersJson,

  [string] $BuildParametersJson,

  [int] $PollIntervalSeconds = 300,

  [string] $TokenResource = '499b84ac-1321-427f-aa17-267ca6975798',

  [int] $PollTimeoutMinutes = 0,

  [switch] $NoWait
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

# Import shared logic unless a caller (e.g. a test harness) has already provided it.
if (-not (Get-Command 'LogInfo' -ErrorAction SilentlyContinue)) {
  . (Join-Path $PSScriptRoot ".." "common" "scripts" "logging.ps1")
}
if (-not (Get-Command 'Start-DevOpsBuild' -ErrorAction SilentlyContinue)) {
  . (Join-Path $PSScriptRoot ".." "common" "scripts" "Invoke-DevOpsAPI.ps1")
}
if (-not (Get-Command 'Set-PipelineVariable' -ErrorAction SilentlyContinue)) {
  function Set-PipelineVariable {
    param([string]$Name, [string]$Value)
    if ((Get-Command 'Test-SupportsDevOpsLogging' -ErrorAction SilentlyContinue) -and (Test-SupportsDevOpsLogging)) {
      Write-Host "##vso[task.setvariable variable=$Name]$Value"
    }
  }
}

# Acquire a fresh Azure DevOps bearer token from the az CLI. Guarded so a test harness can stub it.
if (-not (Get-Command 'Get-AzDevOpsToken' -ErrorAction SilentlyContinue)) {
  function Get-AzDevOpsToken {
    param([string]$TokenResource)
    if (-not (Get-Command 'az' -ErrorAction SilentlyContinue)) { return $null }
    $token = az account get-access-token --resource $TokenResource --query accessToken -o tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token)) { return $null }
    return $token.Trim()
  }
}

# Safe property accessor: under Set-StrictMode -Version 2.0+ referencing a non-existent
# property throws, so guard reads of the externally-provided config object.
function Get-Prop($obj, [string]$name, $default = $null) {
  if ($null -ne $obj -and ($obj.PSObject.Properties.Name -contains $name)) {
    return $obj.$name
  }
  return $default
}

# Fetch a single build's current state. Defined inline (guarded) so a test harness can stub it.
if (-not (Get-Command 'Get-DevOpsBuild' -ErrorAction SilentlyContinue)) {
  function Get-DevOpsBuild {
    param(
      $Organization = 'azure-sdk',
      $Project = 'internal',
      [Parameter(Mandatory = $true)] $BuildId,
      $Base64EncodedToken = $null,
      $BearerToken = $null
    )
    $uri = $DevOpsAPIBaseURI -F $Organization, $Project, "build", "builds/$BuildId", ""
    $headers = (Get-DevOpsApiHeaders -Base64EncodedToken $Base64EncodedToken -BearerToken $BearerToken)
    return Invoke-RestMethod -Method GET -Uri $uri -Headers $headers -MaximumRetryCount 3
  }
}

# Poll a queued build until it completes, then return the final build object.
function Wait-DevOpsBuild {
  param(
    [Parameter(Mandatory = $true)] $Organization,
    [Parameter(Mandatory = $true)] $Project,
    [Parameter(Mandatory = $true)] $BuildId,
    [string] $BearerToken,
    [string] $Base64EncodedToken,
    [int] $PollIntervalSeconds = 300,
    [int] $MaxConsecutiveErrors = 5,
    [int] $PollTimeoutMinutes = 0,
    [scriptblock] $TokenRefresh = $null
  )

  $token = $BearerToken
  $consecutiveErrors = 0
  $startTime = Get-Date

  while ($true) {
    $build = $null
    $status = $null
    try {
      $build = Get-DevOpsBuild -Organization $Organization -Project $Project -BuildId $BuildId `
        -BearerToken $token -Base64EncodedToken $Base64EncodedToken
      $status = Get-Prop $build 'status' $null
    }
    catch {
      LogWarning "  Failed to query build $BuildId status: $_"
    }

    if ($status -eq 'completed') {
      return $build
    }

    if ([string]::IsNullOrWhiteSpace($status) -or $status -eq 'unknown') {
      # A missing/unknown status usually means the access token expired and DevOps returned a
      # sign-in response instead of the build. Try to refresh the token and retry, and give up
      # after too many consecutive failures so we never poll forever.
      $consecutiveErrors++
      LogWarning "  Build $BuildId returned no valid status ($consecutiveErrors/$MaxConsecutiveErrors) - the access token may have expired."

      if ($TokenRefresh) {
        $fresh = & $TokenRefresh
        if (-not [string]::IsNullOrWhiteSpace($fresh)) {
          $token = $fresh
          LogInfo "  Refreshed the access token; will retry the status check."
        }
      }

      if ($consecutiveErrors -ge $MaxConsecutiveErrors) {
        throw "Gave up polling build $BuildId after $consecutiveErrors consecutive failed status checks (the auth token likely expired and could not be refreshed)."
      }
    }
    else {
      $consecutiveErrors = 0
      LogInfo "  Build $BuildId status: $status. Polling again in $PollIntervalSeconds second(s)..."
    }

    if ($PollTimeoutMinutes -gt 0 -and ((Get-Date) - $startTime).TotalMinutes -ge $PollTimeoutMinutes) {
      throw "Timed out waiting for build $BuildId to complete after $PollTimeoutMinutes minute(s)."
    }

    Start-Sleep -Seconds $PollIntervalSeconds
  }
}

# --- Resolve config ---------------------------------------------------------
$config = $null
if (Test-Path $ConfigPath) {
  try {
    $config = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json
  }
  catch {
    LogError "Failed to parse config file '$ConfigPath': $_"
    exit 1
  }
}

$resolvedOrg = if ($Organization) { $Organization } else { Get-Prop $config 'organization' 'azure-sdk' }
$resolvedProject = if ($Project) { $Project } else { Get-Prop $config 'project' 'internal' }

# --- Resolve the list of pipelines to trigger -------------------------------
$targets = @()

if (-not [string]::IsNullOrWhiteSpace($DefinitionIds)) {
  foreach ($idStr in ($DefinitionIds -split '[,\s]+' | Where-Object { $_ })) {
    $idVal = 0
    if (-not [int]::TryParse($idStr.Trim(), [ref]$idVal)) {
      LogWarning "Ignoring invalid definition id '$idStr' (not an integer)."
      continue
    }
    $targets += [pscustomobject]@{ DefinitionId = $idVal; Name = "Definition $idVal" }
  }
}
elseif ($null -ne $config) {
  foreach ($p in (Get-Prop $config 'pipelines' @())) {
    $idVal = [int](Get-Prop $p 'definitionId' 0)
    $targets += [pscustomobject]@{ DefinitionId = $idVal; Name = (Get-Prop $p 'name' "Definition $idVal") }
  }
}
else {
  LogError "No pipelines specified. Provide -DefinitionIds or a config file at '$ConfigPath'."
  exit 1
}

$validTargets = @($targets | Where-Object { $_.DefinitionId -gt 0 })
$placeholderCount = $targets.Count - $validTargets.Count
if ($placeholderCount -gt 0) {
  LogWarning "Skipping $placeholderCount pipeline entr$(if ($placeholderCount -eq 1) { 'y' } else { 'ies' }) with a missing or non-positive definitionId (placeholder)."
}

if ($validTargets.Count -eq 0) {
  LogError "No valid pipeline definition IDs to trigger. Provide -DefinitionIds or configure real definitionId values in '$ConfigPath'."
  exit 1
}

# --- Trigger each pipeline --------------------------------------------------
# By default the configured pipelines cannot run in parallel, so trigger one, poll its run
# until it completes, then trigger the next. With -NoWait, trigger them back-to-back without
# waiting for any run to finish.
$triggerMode = if ($NoWait) { "without waiting for completion" } else { "sequentially, waiting for each to complete" }
LogInfo "Triggering $($validTargets.Count) pipeline(s) in $resolvedOrg/$resolvedProject $triggerMode."

# Long sequential runs can outlive a single access token (~60-90 min), so refresh the bearer
# token (when possible) before queuing each pipeline and while polling for completion.
$currentBearerToken = $BearerToken
$tokenRefresh = {
  if (-not [string]::IsNullOrWhiteSpace($TokenResource) -and (Get-Command 'Get-AzDevOpsToken' -ErrorAction SilentlyContinue)) {
    return (Get-AzDevOpsToken -TokenResource $TokenResource)
  }
  return $null
}.GetNewClosure()

$results = @()
foreach ($target in $validTargets) {
  $label = "$($target.Name) (definition $($target.DefinitionId)) in $resolvedOrg/$resolvedProject"

  if (-not $PSCmdlet.ShouldProcess($label, "Queue pipeline and wait for completion")) {
    $results += [pscustomobject]@{ DefinitionId = $target.DefinitionId; Name = $target.Name; Status = 'Skipped (WhatIf)'; Result = ''; Url = '' }
    continue
  }

  # Refresh the access token before each pipeline so long sequential runs don't outlive it.
  if ($currentBearerToken) {
    $fresh = & $tokenRefresh
    if (-not [string]::IsNullOrWhiteSpace($fresh)) { $currentBearerToken = $fresh }
  }

  $queueArgs = @{
    Organization = $resolvedOrg
    Project      = $resolvedProject
    SourceBranch = $SourceBranch
    DefinitionId = $target.DefinitionId
  }
  if ($currentBearerToken) { $queueArgs.BearerToken = $currentBearerToken }
  if ($Base64EncodedToken) { $queueArgs.Base64EncodedToken = $Base64EncodedToken }
  if ($TemplateParametersJson) { $queueArgs.TemplateParametersJson = $TemplateParametersJson }
  if ($BuildParametersJson) { $queueArgs.BuildParametersJson = $BuildParametersJson }

  $resp = $null
  try {
    $resp = Start-DevOpsBuild @queueArgs
  }
  catch {
    LogError "Failed to queue definition $($target.DefinitionId): $_"
    $results += [pscustomobject]@{ DefinitionId = $target.DefinitionId; Name = $target.Name; Status = 'Failed to queue'; Result = ''; Url = '' }
    continue
  }

  $buildId = Get-Prop $resp 'id'
  $url = Get-Prop (Get-Prop (Get-Prop $resp '_links') 'web') 'href' ''
  $name = Get-Prop (Get-Prop $resp 'definition') 'name' $target.Name

  LogSuccess "Queued [$name] (definition $($target.DefinitionId), build $buildId) -> $url"

  if ($NoWait) {
    $results += [pscustomobject]@{ DefinitionId = $target.DefinitionId; Name = $name; Status = 'Queued'; Result = '(not waited)'; Url = $url }
    continue
  }

  LogInfo "Waiting for build $buildId to finish before triggering the next pipeline..."

  try {
    $final = Wait-DevOpsBuild -Organization $resolvedOrg -Project $resolvedProject -BuildId $buildId `
      -BearerToken $currentBearerToken -Base64EncodedToken $Base64EncodedToken -PollIntervalSeconds $PollIntervalSeconds `
      -PollTimeoutMinutes $PollTimeoutMinutes -TokenRefresh $tokenRefresh

    $result = Get-Prop $final 'result' 'unknown'
    if ($result -eq 'succeeded') {
      LogSuccess "Build $buildId ([$name]) completed with result: $result"
    }
    else {
      LogWarning "Build $buildId ([$name]) completed with result: $result"
    }
    $results += [pscustomobject]@{ DefinitionId = $target.DefinitionId; Name = $name; Status = 'Completed'; Result = $result; Url = $url }
  }
  catch {
    LogError "Failed while polling build $buildId (definition $($target.DefinitionId)): $_"
    $results += [pscustomobject]@{ DefinitionId = $target.DefinitionId; Name = $name; Status = 'Poll failed'; Result = ''; Url = $url }
  }
}

# --- Summary ----------------------------------------------------------------
LogInfo "Trigger summary:"
($results | Format-Table DefinitionId, Name, Status, Result, Url -AutoSize | Out-String).TrimEnd() | Write-Host

$runLinks = @($results | Where-Object { $_.Url } |
    ForEach-Object { "[$($_.Name)]($($_.Url))" })
if ($runLinks.Count -gt 0) {
  Set-PipelineVariable -Name 'QueuedPipelineLinks' -Value ($runLinks -join '<br>')
}

# A pipeline counts as failed if it could not be queued, polling failed, or the run
# did not finish with a 'succeeded' result.
$failures = @($results | Where-Object {
    $_.Status -eq 'Failed to queue' -or
    $_.Status -eq 'Poll failed' -or
    ($_.Status -eq 'Completed' -and $_.Result -ne 'succeeded')
  })
$failureCount = $failures.Count
if ($failureCount -gt 0) {
  LogError "$failureCount pipeline(s) did not succeed."
}

exit $failureCount
