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
    [string] $RepoOrg,

    [Parameter(Mandatory = $true)]
    [ValidateSet('github', 'git')]
    [string] $RepoType,

    [string] $CommitSHA="master",

    [Parameter(Mandatory = $true)]
    [string] $RepoProjectID,

    [Parameter(Mandatory = $true)]
    [string] $GitHubPAT,
    [Parameter(Mandatory = $true)]
    [string] $AzureGitPAT
)

Write-Host $MyInvocation.Line

$HttpHeaders = @{}

if ($RepoType -eq 'github')
{
    $RepoApiURL = "https://api.github.com/repos/$RepoOrg/$RepoName"
    $CommitsURL = "$RepoApiURL/commits/$CommitSHA"
    $CreateTagRefURL = "$RepoApiURL/git/refs"
    $ListTagsURL = "$RepoApiURL/git/refs/tags"
}
else {
    # It's an Azure git Repo in DevOps
    $APIVersion = "api-version=5.1"
    $RepoApiURL = "https://dev.azure.com/$RepoOrg/$RepoProjectID/_apis/git/repositories/$RepoName"

    $ItemVersionType = "branch"
    if ($CommitSHA -ne "master") { $ItemVersionType = "commit" }

    $CommitsURL = "$RepoApiURL/commits?searchCriteria." + '$top' + "=1&searchCriteria.itemVersion.versionType=$ItemVersionType&searchCriteria.itemVersion.version=$CommitSHA&$APIVersion"
    Write-Host $CommitsURL
    $CreateTagRefURL = "$RepoApiURL/refs?$APIVersion"
    $ListTagsURL = "$RepoApiURL/refs?filter=tags/&$APIVersion"

    # Configure Authorization Header
    $Encoder = [System.Text.ASCIIEncoding]::new()
    $Credentials = "Chidozie Ononiwu" + ":" + $AzureGitPAT
    $NoofBytesRequired = $Encoder.GetByteCount($Credentials)
    $CredentialsEncoded = [System.Byte[]]::new($NoofBytesRequired)
    $NoOfBytesWriten = $Encoder.GetBytes($Credentials, 0, $CredentialsEncoded.Length, $CredentialsEncoded, 0)
    $CredentialsBase64 = [System.Convert]::ToBase64String($CredentialsEncoded);
    $HttpHeaders.Add("Authorization", "Basic $CredentialsBase64")
}

$CurrentDate = Get-Date -Format "yyyyMMdd"
$TagHead = $RepoName + "_" + $CurrentDate + "."

Import-Module "$PSScriptRoot/../../eng/common/scripts/modules/git-api-calls.psm1"

try {
    $CurrentTagsInRepo = GetExistingTags -apiUrl $ListTagsURL -repoType $RepoType -headers $HttpHeaders
}
catch {
    Write-Error "$_"
    Write-Error "Ensure that the RepoName and RepoOrg are correct."
    Exit 1
}

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

try {
    $CommitToTag = FireAPIRequest -url $CommitsURL -method "Get" -headers $HttpHeaders -repoType $RepoType
    $CommitToTagSHA = $null
    if ($RepoType -eq 'github')
    {
        $CommitToTagSHA = $CommitToTag.sha
    }
    else {
        $CommitToTagSHA = $CommitToTag.value.commitId
    }
}
catch {
    Write-Error "$_"
    Write-Error "Ensure that the specified commit SHA exist in the specified repository"
    Exit 1
}

if ($RepoType -eq 'github')
{
    $TagBody = @{}
    $TagBody.Add("ref", "refs/tags/$FullTag")
    $TagBody.Add("sha", $CommitToTagSHA)
    $HttpHeaders.Add("Content-Type", "application/json")
    $HttpHeaders.Add("Authorization", "token $GitHubPAT")
}
else {
    $TagObj = @{}
    $TagObj.Add("name", "refs/tags/$FullTag")
    $TagObj.Add("oldObjectId", "0000000000000000000000000000000000000000")
    $TagObj.Add("newObjectId", $CommitToTagSHA)
    $TagBody = @($TagObj)
    $HttpHeaders.Add("Content-Type", "application/json")
}

$TagBodyJson = ConvertTo-Json $TagBody

$RequestResponse = FireAPIRequest -url $CreateTagRefURL -body $TagBodyJson -headers $HttpHeaders -method "Post" -repoType $RepoType