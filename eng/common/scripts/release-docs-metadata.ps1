# Note, due to how `Expand-Archive` is leveraged in this script,
# powershell core is a requirement for successful execution.
param (
  # arguments leveraged to parse and identify artifacts
  $ArtifactLocation, # the root of the artifact folder. DevOps $(System.ArtifactsDirectory)
  $WorkDirectory, # a clean folder that we can work in
  $ReleaseSHA, # the SHA for the artifacts. DevOps: $(Release.Artifacts.<artifactAlias>.SourceVersion) or $(Build.SourceVersion)
  $RepoId, # full repo id. EG azure/azure-sdk-for-net  DevOps: $(Build.Repository.Id). Used as a part of VerifyPackages
  $Repository, # EG: "Maven", "PyPI", "NPM"

  # arguments necessary to power the docs release
  $Language, # EG: js, java, dotnet. Used in language for the embedded readme.
  $TargetDocRepo, # the repository that we will be writing the readmes to 
  $LatestBranch = "smoke-test", # normally `smoke-test`
  $PreviewBranch = "smoke-test-preview", # normally `smoke-test-preview`
  $DocRepoContentLocation = "docs-ref-services/" # within the doc repo, where does our readme go?
)

# import artifact parsing and semver handling
. (Join-Path $PSScriptRoot artifact-metadata-parsing.ps1)
. (Join-Path $PSScriptRoot SemVer.ps1)

# normally best case is to stay away from `Invoke-Expression`
# however, given that we have a few git commands to get through and simplicity of script has a value of it's own
# this works in this limited context.
function _execute($command, $hardErr = $True){
  try {
    Write-Host $command
    Invoke-Expression "&$command"
    if ($LastExitCode -ne 0) {
      Write-Host "Command exited with error code $LastExitCode"
    }
  }
  catch {
    WriteHost $_
    if ($hardErr) {
      exit(1)
    }
  }
}

function SyncDocRepo($docRepo, $workingDirectory, $sourceBranch, $prBranch) {
  # get the doc repo, looking for the rdm branch if it exists
  $docRepo = "https://github.com/$docRepo"
  $repoRoot = (Join-Path $workingDirectory "repo/" )

  _execute("git clone $docRepo $repoRoot")

  try {
    Push-Location $repoRoot

    # 0 if exists, 1 if not
    git show-ref --verify --quiet refs/heads/$sourceBranch
    $sourceBranchExists = $LastExitCode
    git show-ref --verify --quiet refs/heads/$prBranch
    $prBranchExists = $LastExitCode

    if (-not $prBranchExists) {
      # already exists, so just grab it
      _execute("git checkout $sourceBranch")
    }
    else {
      if (-not $sourceBranchExists) {
        # there is an existing smoke-test branch we can base off of
        # Write-Host "git checkout $sourceBranch"
        _execute("git checkout $sourceBranch")
        # Write-Host "git checkout -b $prBranch"
        _execute("git checkout -b $prBranch")
      }
      else {
        # smoke-test branch doesn't exist, so default to base off of master (this is the safe option)
        _execute("git checkout -b $prBranch")
      }
    }
  } finally {
    Pop-Location
  }

  return $repoRoot
}

function GetMetaData($lang){
  switch ($lang) {
    "java" {
      $metadataUri = "https://raw.githubusercontent.com/Azure/azure-sdk/master/_data/releases/latest/java-packages.csv"
      break
    }
    ".net" {
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

    # the namespace is not expected to be present for js.
    $pkgId = $pkgInfo.PackageId.Replace("@azure/", "")

    try {
      $metadata = GetMetaData -lang $lang 
      $service = $metadata | ? { $_.Package -eq $pkgId }

      if ($service) {
        $service = "$service,"
      }
    }
    catch {
      Write-Host $_
      Write-Host "Unable to retrieve service metadata for packageId $($pkgInfo.PackageId)"
    }

    $headerContentMatch = (Select-String -InputObject $pkgInfo.ReadmeContent -Pattern 'Azure .+? (client|plugin|shared) library for (JavaScript|Java|Python|\.NET|C)').Matches[0]

    if ($headerContentMatch){
      $header = "---`r`ntitle: $headerContentMatch`r`nkeywords: Azure, $lang, SDK, API, $service $($pkgInfo.PackageId)`r`nauthor: maggiepint`r`nms.author: magpint`r`nms.date: $date`r`nms.topic: article`r`nms.prod: azure`r`nms.technology: azure`r`nms.devlang: $lang`r`nms.service: $service`r`n---`r`n"
      $fileContent = $pkgInfo.ReadmeContent -replace $headerContentMatch, "$headerContentMatch - Version $($pkgInfo.PackageVersion) `r`n"
      return "$header $fileContent"
    }
    else {
      return ""
    }
}

$apiUrl = "https://api.github.com/repos/$repoId"
$pushUrl = "https://$($env:GH_TOKEN)@github.com/$TargetDocRepo.git"
$branchScriptLocation = (Join-Path $PSScriptRoot git-branch-push.ps1)
$prScriptLocation = (Join-Path $PSScriptRoot Submit-PullRequest.ps1)
$DocRepoOwner = $TargetDocRepo.split("/")[0]
$DocRepoName = $TargetDocRepo.split("/")[1]

$pkgs = VerifyPackages -pkgRepository $Repository `
  -artifactLocation $ArtifactLocation `
  -workingDirectory $WorkDirectory `
  -apiUrl $apiUrl `
  -releaseSha $ReleaseSHA `
  -exitOnError $False

if ($pkgs) {
  Write-Host "Given the visible artifacts, readmes will be copied for the following packages"
  Write-Host ($pkgs | % { $_.PackageId }) 

  foreach ($packageInfo in $pkgs) {
    # ensure the working directory is clean for the doc repo clone
    Remove-Item -Recurse -Force "$WorkDirectory/*"
    
    # sync the doc repo
    $semVer = [AzureEngSemanticVersion]::ParseVersionString($packageInfo.PackageVersion)
    $rdSuffix = ""
    $targetDocBranch = $LatestBranch
    if ($semVer.IsPreRelease) {
      $targetDocBranch = $PreviewBranch
      $rdSuffix = "-pre"
    }
    $prBranch = "$targetDocBranch-rdme"
    $repoLocation = SyncDocRepo -docRepo $TargetDocRepo -workingDirectory $WorkDirectory -sourceBranch $targetDocBranch -prBranch $prBranch

    Write-Host "Selected Branch is $targetDocBranch, PRBranch is $prBranch"

    $readmeName = "$($packageInfo.PackageId.Replace('azure-','').Replace('Azure.', '').Replace('@azure/', '').ToLower())-readme$rdSuffix.md"
    $readmeLocation = Join-Path $repoLocation $DocRepoContentLocation $readmeName
    $adjustedContent = GetAdjustedReadmeContent -pkgInfo $packageInfo -lang $Language

    if ($adjustedContent) {
      try {
        Push-Location $repoLocation
        Set-Content -Path $readmeLocation -Value $adjustedContent -Force

        Write-Host "git add -A ."
        git add -A .

        # commit the changes to the PR Branch. If others have pushed to the target branch we want to rebase off it and commit our changes regardless
        & $branchScriptLocation -PRBranchName $prBranch `
          -CommitMsg "Updating for release of $($packageInfo.PackageId) version $($packageInfo.PackageVersion)" `
          -GitUrl $pushUrl

        # attempt the PR submit pullrequest
        & $prScriptLocation -RepoOwner $DocRepoOwner -RepoName $DocRepoName -BaseBranch `
          $targetDocBranch -PROwner $DocRepoOwner -PRBranch $prBranch -AuthToken $($env:GH_TOKEN) -PRTitle "Docs Readme Update"
      } catch {
        Write-Host $_
      } finally {
        Pop-Location
      }
    } else {
      Write-Host "Unable to parse a header out of the readmecontent for PackageId $($packageInfo.PackageId)"
    }
  }
}
else {
  Write-Host "No readmes discovered for doc release under folder $ArtifactLocation."
}
