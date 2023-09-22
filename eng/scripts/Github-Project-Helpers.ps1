function Get-GithubProjectId([string] $project)
{
  # project should be ine one of the following formats
  # https://github.com/orgs/<org>/projects/<number>
  # https://github.com/users/<user>/projects/<number>
  # or just a number in which case default to Azure as the org
  $projectId = ""
  if ($project -match "(((orgs/(?<org>.*))|(users/(?<user>.*)))/projects/)?(?<number>\d+)$")
  {
    $projectNumber = $matches["number"]
    if ($matches["user"]) {
      $name = $matches["user"]
      $projectQuery = 'query($name: String!, $number: Int!) { user(login: $name) { projectV2(number: $number) { id } } }'
      $selectQuery = ".data.user.projectV2.id"
    }
    else {
      $name = $matches["org"]
      $name ??= "Azure"

      $projectQuery = 'query($name: String!, $number: Int!) { organization(login: $name) { projectV2(number: $number) { id } } }'
      $selectQuery = ".data.organization.projectV2.id"
    }

    $projectId = gh api graphql -f query=$projectQuery -F name=$name -F number=$projectNumber --jq $selectQuery

    if ($LASTEXITCODE) {
      Write-Error "$projectId`nLASTEXITCODE = $LASTEXITCODE"
    }
  }
  return $projectId
}

function Add-GithubIssueToProject([string]$projectId, [string]$issueId)
{
  $projectItemId = gh api graphql -F projectId=$projectId -F issueId=$issueId -f query='
    mutation($projectId: ID!, $issueId: ID!) {
      addProjectV2ItemById(input: {projectId: $projectId, contentId: $issueId}) {
        item {
          id
        }
      }
    }' --jq ".data.addProjectV2ItemById.item.id"

  if ($LASTEXITCODE) {
    Write-Error "$projectItemId`nLASTEXITCODE = $LASTEXITCODE"
  }
  return $projectItemId
}

function Remove-GithubIssueFromProject([string]$projectId, [string]$projectItemId)
{
  $projectDeletedItemId = gh api graphql -F projectId=$projectId -F itemId=$projectItemId -f query='
    mutation($projectId: ID!, $itemId: ID!)  {
      deleteProjectV2Item(input: {projectId: $projectId, itemId: $itemId} ) {
        deletedItemId
      }
  }' --jq ".data.deleteProjectV2Item.deletedItemId"

  if ($LASTEXITCODE) {
    Write-Error "$projectDeletedItemId`nLASTEXITCODE = $LASTEXITCODE"
  }
  return $projectDeletedItemId
}

function Get-BranchProtectionChecks([string]$org = "Azure", [string]$repo, [string]$branch = "main")
{
  $resp = gh api -H "Accept: application/vnd.github+json" /repos/$org/$repo/branches/$branch/protection/required_status_checks
  if ($LASTEXITCODE) {
      exit $LASTEXITCODE
  }
  return $resp | ConvertFrom-Json -AsHashtable -Depth 100
}

function Remove-BranchProtectionCheck()
{
  [CmdletBinding(SupportsShouldProcess)]
  param(
    [string]$org = "Azure",
    [string]$repo,
    [string]$branch = "main",
    [string]$ruleName
  )
  $ErrorActionPreference = 'Stop'

  $rules = Get-BranchProtectionChecks -org $org -repo $repo -branch $branch
  $dateTag = Get-Date -Format "yyyyMMdd.hhmmss"
  $backupFile = "$org.$repo.$branch.required-checks.$dateTag.json"
  Write-Host "Backing up original rules to $backupFile"
  $rules | ConvertTo-Json -Depth 100 | Out-File -WhatIf:$false $backupFile

  $rules.checks = [array]($rules.checks | Where-Object { $_.context -ne $ruleName })
  # Deprecated, but update for parity just in case
  $rules.contexts = [array]($rules.contexts | Where-Object { $_ -ne $ruleName })
  if (!$rules.checks) {
      $rules.checks = @()
  }
  if (!$rules.contexts) {
      $rules.contexts = @()
  }

  Write-Host "Removing check '$ruleName'"
  Write-Host "New rules: $($rules.checks | ConvertTo-Json -Compress)"
  $body = $rules | ConvertTo-Json -Depth 100 -Compress
  if($PSCmdlet.ShouldProcess("Remove check $ruleName for ${branch}?")) {
    $_ = $body | gh api -X PATCH -H "Accept: application/vnd.github+json" /repos/$org/$repo/branches/$branch/protection/required_status_checks --input -
    if ($LASTEXITCODE) {
      exit $LASTEXITCODE
    }
  }
}

function Add-BranchProtectionCheck()
{
  [CmdletBinding(SupportsShouldProcess)]
  param(
    [string]$org = "Azure",
    [string]$repo,
    [string]$branch = "main",
    [string]$ruleName
  )
  $ErrorActionPreference = 'Stop'

  $rules = Get-BranchProtectionChecks -org $org -repo $repo -branch $branch
  $dateTag = Get-Date -Format "yyyyMMdd.hhmmss"
  $backupFile = "$org.$repo.$branch.required-checks.$dateTag.json"
  Write-Host "Backing up original rules to $backupFile"
  $rules | ConvertTo-Json -Depth 100 | Out-File -WhatIf:$false $backupFile

  $rules.checks = [array]($rules.checks | Where-Object { $_.context -ne $ruleName -and !$_.app_id })
  # Deprecated, but update for parity just in case
  $rules.contexts = [array]($rules.contexts | Where-Object { $_ -ne $ruleName })
  if (!$rules.checks) {
      $rules.checks = @()
  }
  if (!$rules.contexts) {
      $rules.contexts = @()
  }

  if ($rules.checks.context -notcontains $ruleName) {
   # For now, support "any source" only for required check source
   # Setting the `app_id` field to -1 results in "any source" being set for the check
   $rules.checks = $rules.checks + @( @{ context = $ruleName ; app_id = -1 } )
   # Deprecated, but update for parity just in case
   $rules.contexts = $rules.contexts + $ruleName
  } else {
    Write-Host "Rule $ruleName already exists for $branch"
    return
  }

  Write-Host "New rules: $($rules.checks | ConvertTo-Json -Compress)"
  $body = $rules | ConvertTo-Json -Depth 100 -Compress
  if($PSCmdlet.ShouldProcess("Add rule $ruleName for ${branch}?")) {
    $_ = $body | gh api -X PATCH -H "Accept: application/vnd.github+json" /repos/$org/$repo/branches/$branch/protection/required_status_checks --input -
    if ($LASTEXITCODE) {
      exit $LASTEXITCODE
    }
  }
}
