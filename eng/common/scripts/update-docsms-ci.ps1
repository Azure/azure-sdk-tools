# Note, due to how `Expand-Archive` is leveraged in this script,
# powershell core is a requirement for successful execution.


param (
  # arguments leveraged to parse and identify artifacts
  [Parameter(Mandatory = $true)]
  $ArtifactLocation, # the root of the artifact folder. DevOps $(System.ArtifactsDirectory)
  
  [Parameter(Mandatory = $true)]
  $WorkDirectory, # a clean folder that we can work in
  
  [Parameter(Mandatory = $true)]
  $ReleaseSHA, # the SHA for the artifacts. DevOps: $(Release.Artifacts.<artifactAlias>.SourceVersion) or $(Build.SourceVersion)
  
  [Parameter(Mandatory = $true)]
  $RepoId, # full repo id. EG azure/azure-sdk-for-net  DevOps: $(Build.Repository.Id). Used as a part of VerifyPackages
  
  [Parameter(Mandatory = $true)]
  [ValidateSet("Nuget","NPM","PyPI","Maven")]
  $Repository, # EG: "Maven", "PyPI", "NPM"

  # Used for All Languages
  [Parameter(Mandatory = $true)]
  $CIRepository,

  [Parameter(Mandatory = $true)]
  $CIAccessPAT,

  [Parameter(Mandatory = $false)]
  [ValidateSet("Latest","Preview")]
  $Mode,

  # arguments specific to each language follow
  # Java
  $MonikerIdentifier,

  # C# 
  $PathToConfigFile,
  
  # JS and Python only
  $ApiUrl,
  $BuildId,
  $TargetVariable="params" # standard template CI deployments will use "params" as the name of the variable
)

# import artifact parsing and semver handling
. (Join-Path $PSScriptRoot artifact-metadata-parsing.ps1)
. (Join-Path $PSScriptRoot SemVer.ps1)


# for JavaScript and Python, the onboarded packages are targeted in a params.json present
# as a build variable. Example Schema:
# {
#   "target_repo": {
#     "url": "https://github.com/MicrosoftDocs/azure-docs-sdk-python",
#     "branch": "smoke-test-preview",
#     "folder": "preview"
#   },
#   "packages": [
#     {
#       "package_info": {
#         "install_type": "dist_file",
#         "name": "azure-core-tracing-opencensus",
#         "version": ">=1.0.0b4",
#         "location": "https://docsupport.blob.core.windows.net/repackaged/azure-core-tracing-opencensus-1.0.0b6.zip"
#       },
#       "exclude_path": [
#         "test*",
#         "example*",
#         "sample*",
#         "doc*"
#       ]
#     },
#     ...
#   ]
# }
function UpdateJSCI($pkgs, $pat, $ciRepo, $locationInDocRepo, $ApiUrl, $BuildId, $TargetVariable){
  # access key is values["packages"] -> package list
}

function UpdatePythonCI($pkgs, $pat, $ciRepo, $locationInDocRepo, $ApiUrl, $BuildId, $TargetVariable){
  
}


# details on CSV schema can be found here:
# https://review.docs.microsoft.com/en-us/help/onboard/admin/reference/dotnet/documenting-nuget?branch=master#set-up-the-ci-job
function UpdateCSVBasedCI($pkgs, $pat, $ciRepo, $locationInDocRepo){

}

# a "package.json configures target packages for all the monikers in a Repository, it also has a slightly different
# schema than the moniker-specific json config
function UpdatePackageJson($pkgs, $pat, $ciRepo, $locationInDocRepo, $monikerId){
  Write-Host (Join-Path -Path $ciRepo -ChildPath $locationInDocRepo)
  $pkgJsonLoc = (Join-Path -Path $ciRepo -ChildPath $locationInDocRepo)
  
  if (-not (Test-Path $pkgJsonLoc)) {
    Write-Error "Unable to locate package json at location $pkgJsonLoc, exiting."
    exit(1)
  }

  $allJsonData = Get-Content $pkgJsonLoc | ConvertFrom-Json

  $targetData = $allJsonData[$monikerId]

  $visibleInCI = @{}

  # first pull what's already available
  for ($i=0; $i -lt $targetData.packages.Length; $i++) {
    $pkgDef = $targetData.packages[$i]
    $visibleInCI[$pkgDef.packageArtifactId] = $i
  }

  foreach ($releasingPkg in $pkgs) {
    if ($visibleInCI.ContainsKey($releasingPkg.PackageId)) {
      $packagesIndex = $visibleInCI[$releasingPkg.PackageId]
      $existingPackageDef = $targetData.packages[$packagesIndex]
      $existingPackageDef.packageVersion = $releasingPkg.PackageVersion
    }
    else {
      $newItem = New-Object PSObject -Property @{ 
        packageDownloadUrl = "https://repo1.maven.org/maven2"
        packageGroupId = $releasingPkg.GroupId
        packageArtifactId = $releasingPkg.PackageId
        packageVersion = $releasingPkg.PackageVersion
        inputPath = @()
        excludePath = @()
      }

      $targetData.packages.Append($newItem)
    }
  }

  # update repo content
  Set-Content -Path $pkgJsonLoc -Value ($allJsonData | ConvertTo-Json -Depth 10 | % {$_ -replace "(?m)  (?<=^(?:  )*)", "    " })
}

$apiUrl = "https://api.github.com/repos/$repoId"
$pkgs = VerifyPackages -pkgRepository $Repository `
  -artifactLocation $ArtifactLocation `
  -workingDirectory $WorkDirectory `
  -apiUrl $apiUrl `
  -releaseSha $ReleaseSHA `
  -continueOnError $True 

# filter package set
if($Mode) {
  if ($Mode -eq "Preview") { $includePreview = $true } else { $includePreview = $false }

  $pkgs = $pkgs | ? { $_.isPrerelease -eq $includePreview}
}

if ($pkgs) {
  Write-Host "Given the visible artifacts, CI updates will be processed for the following packages."
  Write-Host ($pkgs | % { $_.PackageId + " " + $_.PackageVersion })

  switch ($Repository) {
    "Nuget" {
      Write-Host "Process C# CI for $packageInfo"
      break
    }
    "NPM" {
      Write-Host "Process Javascript CI for $packageInfo"
      break
    }
    "PyPI" {
      Write-Host "Process Python CI for $packageInfo"
      break
    }
    "Maven" {
      Write-Host "Process Java CI for $packageInfo"
      UpdatePackageJson -pkgs $pkgs -pat $ConfigurationCIPAT -ciRepo $CIRepository -locationInDocRepo $PathToConfigFile -monikerId $MonikerIdentifier 
      break
    }
    default {
      Write-Host "Unrecognized Language: $language"
      exit(1)
    }
  }
}
