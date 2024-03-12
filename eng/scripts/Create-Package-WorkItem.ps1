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
  [string]$PackageType,
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

function Clone-Repo($repoName, $targetDir)
{
    git clone "https://github.com/azure/$repoName.git"  $targetDir
    Write-Host "Cloned repo [$repoName] to [$($targetDir)]"
    if (-not(Test-Path $targetDir))
    {
        Write-Error "Failed to find cloned repo [$($targetDir)] to find pacakge root path from package name. Package root path is required to create or update package work item."
        exit 1
    }
}

function Get-Package-Root-Path($packageName, $language)
{
    # clone language repo
    $repoName = Get-Repo-Name $language
    if (!$repoName)
    {
        Write-Error "GitHub repo name is not found for language [$language]. Failed to find package root path."
        exit 1
    }
    $clonedRepoPath = Join-Path $WorkingDir $repoName
    Clone-Repo $repoName $clonedRepoPath
    
    # load common script helpers from cloned language repo
    . (Join-Path $clonedRepoPath "eng/common/scripts/common.ps1")
    Write-Host "Language repo root path: [$RepoRoot]"
    # Parse package properties from cloned repo and find the package repo path
    $packageProperties = Get-PkgProperties -PackageName $PackageName
    if (!$packageProperties)
    {
        Write-Error "Could not find a package with name [ $packageName ] in repo."
        exit 1
    }

    Write-Host "Package Name [ $($packageProperties.Name) ]"
    return $packageProperties.ServiceDirectory
}

if ($PackageRootPath -eq "")
{
  $PackageRootPath = Get-Package-Root-Path $PackageName $Language
}

Write-Host "Package Root Path: [$PackageRootPath]"
# Create or update package work item  
&$EngCommonScriptsDir/Update-DevOps-Release-WorkItem.ps1 `
    -language $Language `
    -packageName $PackageName `
    -version $PackageVersion `
    -plannedDate $ReleaseDate `
    -packageRepoPath $PackageRootPath `
    -packageType $PackageType `
    -serviceName $ServiceName `
    -packageDisplayName $PackageDisplayName `
    -relatedWorkItemId $RelatedWorkItemId `
    -tag $Tag `
    -devops_pat $Devops_pat