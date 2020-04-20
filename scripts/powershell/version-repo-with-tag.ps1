<#
.DESCRIPTION
Tags latest or specific commit in the specified Repo.
Uses a combination of the Repo Name, Current Date and a Version number to create the tag.
Tag serve as a means of versioning the entire repo.
.PARAMETER RepoName
The name of the repository. EG "azure-sdk-tools"
.PARAMETER RepoOrg
The owning organization of the repository. EG "Azure"
.PARAMETER GitUrl
The GitHub repository URL
.PARAMETER RepoType
Should be either a github or git (Azure) repo
#>
param (
    [Parameter(Mandatory = $true)]
    [string] $RepoName,

    [Parameter(Mandatory = $true)]
    [string] $RepoOrg,

    [Parameter(Mandatory = $true)]
    [ValidateSet('github', 'git')]
    [string] $RepoType,

    [string] $CommitSHA="master",
    [string] $RepoProjectID="fadad8ea-1e34-4d7a-9016-fd29b14e8b94"
)

Write-Host $MyInvocation.Line

if ($RepoType -eq 'github')
{
    $RepoApiURL = "https://api.github.com/repos/$RepoOrg/$RepoName"
    $CommitsURL = "$RepoApiURL/commits/$CommitSHA"
    $CreateTagRefURL = "$RepoApiURL/git/refs"
    $ListTagsURL = "$RepoApiURL/git/refs/tags"
}
else {
    $APIVersion = "api-version=5.1"
    $RepoApiURL = "https://dev.azure.com/$RepoOrg/$RepoProjectID/_apis/git/repositories/$RepoName"
    $CommitsURL = "$RepoApiURL/commits/$CommitSHA/$APIVersion"
    $CreateTagRefURL = "$RepoApiURL/refs?$APIVersion"
    $ListTagsURL = "$RepoApiURL/refs?filter=tags/&$APIVersion"
}


$CurrentDate = Get-Date -Format "yyyyMMdd"
$TagHead = $RepoName + "_" + $CurrentDate + "."

Import-Module "$PSScriptRoot/../../eng/common/scripts/modules/git-api-calls.psm1"

try {
    $CurrentTagsInRepo = GetExistingTags -apiUrl $ListTagsURL
}
catch {
    Write-Error "$_"
    Write-Error "Ensure that the RepoName and RepoOrg are correct."
    Exit 1
}

Write-Host $CurrentTagsInRepo


#$TagsLikeHead = $CurrentTagsInRepo | where { $_ -like "$TagHead*" }
#$TagVersionNo = 1
#
#foreach ($version in $TagsLikeHead)
#{
#    $versionNo = $version.Replace($TagHead, '') -as [int]
#    if ($versionNo -ge $TagVersionNo)
#    {
#        $TagVersionNo = $versionNo + 1
#    }
#}
#
#$FullTag = $TagHead + $TagVersionNo
#
#try {
#    $CommitToTag = FireAPIRequest -url $CommitsURL -method "Get"
#    $CommitToTagSHA = $CommitToTag.sha
#}
#catch {
#    Write-Error "$_"
#    Write-Error "Ensure that the specified commit SHA exist in the specified repository"
#    Exit 1
#}
#
#
#$TagBody = ConvertTo-Json @{
#    ref         = "refs/tags/$FullTag"
#    sha         = $CommitToTagSHA 
#}
#
#$headers = @{
#    "Content-Type"  = "application/json"
#    "Authorization" = "token $($env:GH_TOKEN)"
#}
#
#FireAPIRequest -url $CreateTagRefURL -body $TagBody -headers $headers -method "Post"