# Tags latest or specific commit in the specified Repo.
# Uses a combination of the Repo Name, Current Date and a Version number to create the tag.
# Tag serve as a means of versioning the entire repo.
param (
    [Parameter(Mandatory = $true)]
    [ValidateSet('azure-sdk-tools', 'azure-sdk-build-tools')]
    [string] $RepoName,

    [Parameter(Mandatory = $true)]
    [string] $RepoOrg,

    [Parameter(Mandatory = $true)]
    [ValidateSet('github', 'git')]
    [string] $RepoType,

    [string] $CommitSHA="master"
)

$RepoApiURL = "https://api.github.com/repos/$RepoOrg/$RepoName"
$CommitsURL = "$RepoApiURL/commits/$CommitSHA"
$TagsURL = "$RepoApiURL/git/refs"

$CurrentDate = Get-Date -Format "yyyyMMdd"
$TagHead = $RepoName + "_" + $CurrentDate + "."

Import-Module "$PSScriptRoot/../../eng/common/scripts/modules/git-api-calls.psm1"

$CurrentTagsInRepo = GetExistingTags -apiUrl $RepoApiURL

$TagsLikeHead = $CurrentTagsInRepo | where { $_ -like "$TagHead*" }
$TagVersionNo = 1

foreach ($version in $TagsLikeHead)
{
    $versionNo = $version.Replace($TagHead, '') -as [int]
    if ($versionNo -ge $TagVersionNo)
    {
        $TagVersionNo = $versionNo + 1
    }
}

$FullTag = $TagHead + $TagVersionNo

$LatestCommit = FireAPIRequest -url $CommitsURL -method "Get"
$LatestCommitSHA = $LatestCommit.sha

$TagBody = ConvertTo-Json @{
    ref         = "refs/tags/$FullTag"
    sha         = $LatestCommitSHA 
}

$headers = @{
    "Content-Type"  = "application/json"
    "Authorization" = "token $($env:GH_TOKEN)"
}

FireAPIRequest -url $TagsURL -body $TagBody -headers $headers -method "Post"