[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$Language,
  [Parameter(Mandatory = $true)]
  [string]$ServiceName,
  [Parameter(Mandatory = $true)]
  [string]$PackageName,
  [Parameter(Mandatory = $true)]
  [string]$PackageDisplayName,
  [Parameter(Mandatory = $true)]
  [string]$PackageVersion,
  [Parameter(Mandatory = $true)]
  [string]$ReleaseDate,
  [string]$RelatedWorkItemId,
  [string]$Tag = "",
  [string]$WorkingDir = ".",
  [string]$PackageRootPath = "",
  [string]$Devops_pat = $env:DEVOPS_PAT
)

Set-StrictMode -Version 3

function Get-Repo-Name($language)
{
    switch ($language)
    {
        ".NET" { return "azure-sdk-for-net" }
        "Python" { return "azure-sdk-for-python" }
        "Java" { return "azure-sdk-for-java" }
        "JavaScript" { return "azure-sdk-for-js" }
        "Go" { return "azure-sdk-for-go" }
        "C" { return "azure-sdk-for-c" }
        "C++" { return "azure-sdk-for-cpp" }
    }
    Write-Host "Unknown language to map it to repo name. Language: [$language]"
    return ""
}

function Sparse-Checkout($repoName)
{
    Write-Host "Sparse checkout repo [$repoName]."
    git sparse-checkout init
    git sparse-checkout set "sdk" "eng"
    git checkout
}


$repoName = Get-Repo-Name $language
if (!$repoName)
{
    Write-Error "GitHub repo name is not found for language [$language]. Failed to find package root path."
    exit 1
}

# clone language repo
$clonedRepoPath = Join-Path $WorkingDir $repoName
Write-Host "Cloning repo [$repoName] to [$WorkingDir]."
git clone --no-checkout --filter=tree:0 "https://github.com/azure/$repoName.git" $clonedRepoPath
Push-Location $clonedRepoPath

try
{
    Sparse-Checkout $repoName
    . "eng/common/scripts/common.ps1"

    # Parse package properties from cloned repo and find the package repo path
    $packageProperties = Get-PkgProperties -PackageName $PackageName
    if (!$packageProperties)
    {
        Write-Error "Failed to find package properties for package name [$PackageName]."
        exit 1
    }

    # Create or update package work item  
    &$EngCommonScriptsDir/Update-DevOps-Release-WorkItem.ps1 `
    -language $Language `
    -packageName $PackageName `
    -version $PackageVersion `
    -plannedDate $ReleaseDate `
    -packageRepoPath $PkgProperties.ServiceDirectory `
    -packageType $PkgProperties.SdkType `
    -packageNewLibrary $PkgProperties.IsNewSdk `
    -serviceName $ServiceName `
    -packageDisplayName $PackageDisplayName `
    -relatedWorkItemId $RelatedWorkItemId `
    -tag $Tag `
    -devops_pat $Devops_pat
}
finally {
    Pop-Location
}