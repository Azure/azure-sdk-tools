# cSpell:ignore Committish
# cSpell:ignore PULLREQUEST
# cSpell:ignore TARGETBRANCH
param (
  # The root repo we scanned with.
  [string] $RootRepo = '$PSScriptRoot/../../..',
  # The target branch to compare with.
  [string] $targetBranch = ("origin/${env:SYSTEM_PULLREQUEST_TARGETBRANCH}" -replace "/refs/heads/"),
  # Repository-relative paths containing fixture data rather than published documentation.
  [string[]] $ExcludePaths = @()
)

. (Join-Path $PSScriptRoot common.ps1)

return Get-ChangedFiles -TargetCommittish $targetBranch -DiffPath '*.md' |
  Where-Object { $_ -notin $ExcludePaths }
