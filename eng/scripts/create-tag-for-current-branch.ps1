<#
.SYNOPSIS
.DESCRIPTION
The script is to push current branch to remote tag. It is for backup purpose.
.PARAMETER ReleaseTag
The tag to back up current branch. The format is like mainbackup-2022-08-25
.PARAMETER GithubUrl
The github url for the working repo.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [Parameter(mandatory=$true)]
  [string] $ReleaseTag,
  [Parameter(mandatory=$true)]
  [string] $GithubUrl
)

Set-StrictMode -Version 3

# git push target branch to tag.
Write-Host "git tag $ReleaseTag"
git tag $ReleaseTag

# Always print out the commit SHA
$currentBranch = (git branch --show-current)
$commitSHA = git log -1 --format=tformat:%H 
Write-Host "The $currentBranch last commit SHA is: $commitSHA"

if ($PSCmdlet.ShouldProcess("git push origin $ReleaseTag", "Push local tag to remote tag.")) {
    git push $GithubUrl $ReleaseTag
}
