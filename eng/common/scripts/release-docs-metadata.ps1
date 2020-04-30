# Note, due to how `Expand-Archive` is leveraged in this script,
# powershell core is a requirement for successful execution.
param (
  $ArtifactLocation, # the root of the artifact folder. DevOps $(System.ArtifactsDirectory)
  $WorkDirectory, # a clean folder that we can work in
  $Repository, # EG: "Maven", "PyPI", "NPM"
  $Language,
  $ReleaseSHA, # the SHA for the artifacts. DevOps: $(Release.Artifacts.<artifactAlias>.SourceVersion) or $(Build.SourceVersion)
  $RepoId, # full repo id. EG azure/azure-sdk-for-net  DevOps: $(Build.Repository.Id)
  $TargetDocRepo, # the repository that we will be writing the readmes to 
  $LatestBranch = "smoke-test", # normally `smoke-test`
  $PreviewBranch = "smoke-test-preview", # normally `smoke-test-preview`
  $DocRepoContentLocation = "docs-ref-services/" # within the doc repo, where does our readme go?
)

# import artifact parsing and semver handling
. (Join-Path $PSScriptRoot artifact-metadata-parsing.ps1)
. (Join-Path $PSScriptRoot SemVer.ps1)

function SyncDocRepo($docRepo, $workingDirectory, $sourceBranch) {
  # get the doc repo, looking for the rdm branch if it exists
  $docRepo = "https://github.com/$docRepo"
  $repoRoot = (Join-Path $workingDirectory "repo/" )
  $prBranch = "$sourceBranch-rdme"
  $prBranchExists = (git show-ref --verify --quiet refs/heads/$prBranch)
  $sourceBranchExists = (git show-ref --verify --quiet refs/heads/$sourceBranch)

  Write-Host "Docrepo $docRepo"
  Write-Host "Where to clone $repoRoot"

  git clone $docRepo $repoRoot

  Push-Location $repoRoot

  try {
    if ($prBranchExists) {
      # already exists, so just grab it
      git checkout $sourceBranchExists
    }
    else {
      if (sourceBranchExists) {
        # there is an existing smoke-test branch we can base off of
        git checkout $sourceBranch
        git checkout -b $prBranch
      }
      else {
        # smoke-test branch doesn't exist, so default to base off of master (this is the safe option)
        git checkout -b $prBranch 
      }
    }
  } finally {
    Pop-Location
  }

  return $repoRoot
}

function GetMetaData($lang){
  switch ($lang) {
    "Maven" {
      $metadataUri = "https://raw.githubusercontent.com/Azure/azure-sdk/master/_data/releases/latest/java-packages.csv"
      break
    }
    "Nuget" {
      $metadataUri = "https://raw.githubusercontent.com/Azure/azure-sdk/master/_data/releases/latest/dotnet-packages.csv"
      break
    }
    "python" {
      $metadataUri = "https://raw.githubusercontent.com/Azure/azure-sdk/master/_data/releases/latest/python-packages.csv"
      break
    }
    "js" {
      $metadataUri = "https://raw.githubusercontent.com/Azure/azure-sdk/master/_data/releases/latest/js-packages.csv"
      break
    }
    default {
      Write-Host "Unrecognized Language: $language"
      exit(1)
    }
  }

  $metadataResponse = Invoke-WebRequest -Uri $metadataUri | ConvertFrom-Csv
}

function GetAdjustedReadmeContent($pkgInfo, $lang){
    $date = Get-Date -Format "MM/dd/yyyy"
    $service = ""

    try {
      $metadata = GetMetaData -lang $lang 
      $service = $metadata | ? 

      if ($service) {
        $service = "$service,"
      }
    }
    catch {
      Write-Host $_
      Write-Host "Unable to retrieve service metadata for packageId $($pkgInfo.PackageId)"
    }

    # figure out which service we're dealing within
    $($pkgInfo.PackageId)
    $($pkgInfo.PackageId)

    $fileMatch = (Select-String -InputObject $fileContent -Pattern 'Azure .+? (client|plugin|shared) library for (JavaScript|Java|Python|\.NET|C)').Matches[0]
    $header = "---`r`ntitle: $fileMatch`r`nkeywords: Azure, $lang, SDK, API, $service $($pkgInfo.PackageId)`r`nauthor: maggiepint`r`nms.author: magpint`r`nms.date: $date`r`nms.topic: article`r`nms.prod: azure`r`nms.technology: azure`r`nms.devlang: $lang`r`nms.service: $service`r`n---`r`n"

    $fileContent = $fileContent -replace $fileMatch, "$fileMatch - Version $($pkgInfo.PackageVersion) `r`n"

    return "$header $fileContent"
}

$apiUrl = "https://api.github.com/repos/$repoId"
$pkgs = VerifyPackages -pkgRepository $Repository -artifactLocation $ArtifactLocation -workingDirectory $WorkDirectory -apiUrl $apiUrl -releaseSha $ReleaseSHA -exitOnError $False

# ensure the working directory is clean for the doc repo clone
if ($pkgs) {
  Write-Host "Given the visible artifacts, readmes will be copied for the following packages"
  Write-Host ($pkgs | % { return $_.PackageId })

  foreach ($packageInfo in $pkgs) {
    Remove-Item -Recurse -Force "$WorkDirectory/*"
    
    # # sync the doc repo
    $semVer = [AzureEngSemanticVersion]::ParseVersionString($packageInfo.PackageVersion)
    $targetDocBranch = $LatestBranch
    if ($semVer.IsPreRelease) {
      $targetDocBranch = $PreviewBranch
    }

    $repoLocation = SyncDocRepo -docRepo $TargetDocRepo -workingDirectory $WorkDirectory -sourceBranch $sourceBranch
    $readmeName = "$($packageInfo.PackageId.Replace('azure-','').Replace('Azure.', '').ToLower())-readme.md"
    $readmeLocation = Join-Path $repoLocation $DocRepoContentLocation $readmeName
    $adjustedContent = GetAdjustedReadmeContent -pkgInfo $packageInfo -lang $Language



    Push-Location $repoLocation
    Set-Content -Path $readmeLocation -Value $adjustedContent -Force

    Write-Host "git add -A ."
    Write-Host "git commit -m `"Updating readme with release $($pkgInfo.PackageId) of version $($pkgInfo.PackageVersion)`""

    # try {
    #   # attempt the PR submit pullrequest
    #   # ./Submit-PullRequest.ps1 

    # } catch {
    #   Write-Host $_
    # } finally {
    #   Pop-Location
    # }
  }
}
else {
  Write-Host "No readmes discovered for doc release under folder $ArtifactLocation."
}
