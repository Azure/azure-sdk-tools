<#
.DESCRIPTION
Tags latest or a specific commit in the specified Repo.
Uses a combination of the Repo Name, Current Date and a Version number to create the tag.
Tag serve as a means of versioning the entire repo.
.PARAMETER RepoName
The name of the repository. EG "azure-sdk-tools"
.PARAMETER RepoOrg
The owning organization of the repository. EG "Azure"
.PARAMETER RepoType
Should be either a github or git (Azure) repo
.PARAMETER CommitSHA
The commitSHA to tag. Defaults to master
.PARAMETER RepoProjectID
For git Repos supply the project ID. Default to the internal project ID set by the pipeline
.PARAMETER PATForAPI
Personal Access Token for authentication to the Repo
#>
param (
    [Parameter(Mandatory = $true)]
    [string] $RepoName,

    [Parameter(Mandatory = $true)]
    [string] $SourceDir,

    [string] $CommitSHA="HEAD",
    [string] $PATForAPI,

    [Parameter(Mandatory = $true)]
    [string] $BuildNumber
)

Write-Host $MyInvocation.Line

$VersionTag = $RepoName + "_" + $BuildNumber

pushd "$SourceDir/$RepoName"

Write-Host "Get actual commit SHA"
$CommitSHA = git rev-parse $CommitSHA

Write-Host "Tagging $RepoName with $VersionTag"
git -c user.name="azure-sdk" -c user.email="azuresdk@microsoft.com" tag -a $VersionTag $CommitSHA -m "Append version tag: $VersionTag to $RepoName"

git push https://$PATForAPI@github.com/chidozieononiwu/azure-sdk-tools.git origin/master $VersionTag -v

