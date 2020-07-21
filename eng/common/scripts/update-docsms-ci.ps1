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

  [Parameter(Mandatory = $false)]
  [ValidateSet("Latest","Preview")]
  $Mode,

  # Java leverage a single config file, with a path to the moniker specific config
  # this argument is used as that path.
  $MonikerIdentifier,

  # C#, JS, Python leverage a config file per moniker. need to update multiple
  # paths for preview vs latest
  $PathToConfigFile
)

# import artifact parsing and semver handling
. (Join-Path $PSScriptRoot artifact-metadata-parsing.ps1)
. (Join-Path $PSScriptRoot SemVer.ps1)

# updates json docs.ms CI config for the Python doc repositories
function UpdateParamsJsonPython($pkgs, $ciRepo, $locationInDocRepo, $repository){
  $pkgJsonLoc = (Join-Path -Path $ciRepo -ChildPath $locationInDocRepo)
  
  if (-not (Test-Path $pkgJsonLoc)) {
    Write-Error "Unable to locate package json at location $pkgJsonLoc, exiting."
    exit(1)
  }

  $allJson  = Get-Content $pkgJsonLoc | ConvertFrom-Json
  $targetData = $allJson.packages

  $visibleInCI = @{}

  # first pull what's already available
  for ($i=0; $i -lt $targetData.Length; $i++) {
    $pkgDef = $targetData[$i]

    if ($pkgDef.package_info.name) {
      $visibleInCI[$pkgDef.package_info.name] = $i
    }
  }

  foreach ($releasingPkg in $pkgs) {
    if ($visibleInCI.ContainsKey($releasingPkg.PackageId)) {
      Write-Host "$($releasingPkg.PackageId) is visible in CI"
      Write-Host $targetData[$packagesIndex]
      $packagesIndex = $visibleInCI[$releasingPkg.PackageId]
      $existingPackageDef = $targetData[$packagesIndex]

      if ([AzureEngSemanticVersion]::ParseVersionString($releasingPkg.PackageVersion).IsPrerelease) {
        if (-not $existingPackageDef.package_info.version) {
          $existingPackageDef.package_info | Add-Member -NotePropertyName version -NotePropertyValue ""
        }

        $existingPackageDef.package_info.version = ">=$($releasingPkg.PackageVersion)"
      }
      else {
        if ($def.version) {
          $def.PSObject.Properties.Remove('version')  
        }
      }
    }
    else {
      $newItem = New-Object PSObject -Property @{ 
          package_info = New-Object PSObject -Property @{ 
            prefer_source_distribution = "true"
            install_type = "pypi"
            name=""
          }
          excludePath = @("test*","example*","sample*","doc*")
        }
      Write-Host $targetData

      $targetData.Append($newItem)
    }
  }

  Set-Content -Path $pkgJsonLoc -Value ($allJson | ConvertTo-Json -Depth 10 | % {$_ -replace "(?m)  (?<=^(?:  )*)", "  " })
}

# similar to python CI update. still sets target versions, but is intended for NPM-targeted json
function UpdateParamsJsonJS($pkgs, $ciRepo, $locationInDocRepo, $repository){
  $pkgJsonLoc = (Join-Path -Path $ciRepo -ChildPath $locationInDocRepo)
  
  if (-not (Test-Path $pkgJsonLoc)) {
    Write-Error "Unable to locate package json at location $pkgJsonLoc, exiting."
    exit(1)
  }

  $allJson  = Get-Content $pkgJsonLoc | ConvertFrom-Json
  $targetData = $allJson.npm_package_sources

  $visibleInCI = @{}

  # first pull what's already available
  for ($i=0; $i -lt $targetData.Length; $i++) {
    $pkgDef = $targetData[$i]
    $accessor = ($pkgDef.name).Replace("`@next", "")
    $visibleInCI[$accessor] = $i
  }

  foreach ($releasingPkg in $pkgs) {
    $name = $releasingPkg.PackageId

    if ([AzureEngSemanticVersion]::ParseVersionString($releasingPkg.PackageVersion).IsPrerelease) {
      $name += "`@next"
    }

    if ($visibleInCI.ContainsKey($releasingPkg.PackageId)) {
      $packagesIndex = $visibleInCI[$releasingPkg.PackageId]
      $existingPackageDef = $targetData[$packagesIndex]
      $existingPackageDef.name = $name
    }
    else {
      $newItem = New-Object PSObject -Property @{ 
        name = $name
      }

      if ($newItem) { $targetData.Append($newItem) }
    }
  }

  Set-Content -Path $pkgJsonLoc -Value ($allJson | ConvertTo-Json -Depth 10 | % {$_ -replace "(?m)  (?<=^(?:  )*)", "  " })
}

# details on CSV schema can be found here
# https://review.docs.microsoft.com/en-us/help/onboard/admin/reference/dotnet/documenting-nuget?branch=master#set-up-the-ci-job
function UpdateCSVBasedCI($pkgs, $ciRepo, $locationInDocRepo){
  $csvLoc = (Join-Path -Path $ciRepo -ChildPath $locationInDocRepo)
  
  if (-not (Test-Path $csvLoc)) {
    Write-Error "Unable to locate package csv at location $csvLoc, exiting."
    exit(1)
  }

  $allCSVRows = Get-Content $csvLoc
  $visibleInCI = @{}

  # first pull what's already available
  for ($i=0; $i -lt $allCSVRows.Length; $i++) {
    $pkgDef = $allCSVRows[$i]

    # get rid of the modifiers to get just the package id
    $id = $pkgDef.split(",")[1] -replace "\[.*?\]", ""

    $visibleInCI[$id] = $i
  }

  foreach ($releasingPkg in $pkgs) {
    $installModifiers = "tfm=netstandard2.0"
    if ([AzureEngSemanticVersion]::ParseVersionString($releasingPkg.PackageVersion).IsPrerelease) {
      $installModifiers += ";isPrerelease=true"
    }
    $lineId = $releasingPkg.PackageId.Replace(".","").ToLower()

    if ($visibleInCI.ContainsKey($releasingPkg.PackageId)) {
      Write-Host "Updating"
      $packagesIndex = $visibleInCI[$releasingPkg.PackageId]
      $allCSVRows[$packagesIndex] = "$($lineId),[$installModifiers]$($releasingPkg.PackageId)"
    }
    else {
      Write-Host "Appending"
      $newItem = "$($lineId),[$installModifiers]$($releasingPkg.PackageId)"
      $allCSVRows += ($newItem)
    }
  }

  Set-Content -Path $csvLoc -Value $allCSVRows
}

# a "package.json configures target packages for all the monikers in a Repository, it also has a slightly different
# schema than the moniker-specific json config that is seen in python and js
function UpdatePackageJson($pkgs, $ciRepo, $locationInDocRepo, $monikerId){
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

  Set-Content -Path $pkgJsonLoc -Value ($allJsonData | ConvertTo-Json -Depth 10 | % {$_ -replace "(?m)  (?<=^(?:  )*)", "    " })
}

$apiUrl = "https://api.github.com/repos/$repoId"
$pkgs = VerifyPackages -pkgRepository $Repository `
  -artifactLocation $ArtifactLocation `
  -workingDirectory $WorkDirectory `
  -apiUrl $apiUrl `
  -continueOnError $True 

# filter package set
if($Mode) {
  if ($Mode -eq "Preview") { $includePreview = $true } else { $includePreview = $false }

  $pkgs = $pkgs | ? { [AzureEngSemanticVersion]::ParseVersionString($_.PackageVersion).IsPrerelease -eq $includePreview}
}

if ($pkgs) {
  Write-Host "Given the visible artifacts, CI updates will be processed for the following packages."
  Write-Host ($pkgs | % { $_.PackageId + " " + $_.PackageVersion })

  switch ($Repository) {
    "Nuget" {
      Write-Host "Process C# CI"
      UpdateCSVBasedCI -pkgs $pkgs -ciRepo $CIRepository -locationInDocRepo $PathToConfigFile
      break
    }
    "NPM" {
      Write-Host "Process NPM CI"
      UpdateParamsJsonJS -pkgs $pkgs -ciRepo $CIRepository -locationInDocRepo $PathToConfigFile
      break
    }
    "PyPI" {
      Write-Host "Process Python CI"
      UpdateParamsJsonPython -pkgs $pkgs -ciRepo $CIRepository -locationInDocRepo $PathToConfigFile
      break
    }
    "Maven" {
      Write-Host "Process Java CI"
      UpdatePackageJson -pkgs $pkgs -ciRepo $CIRepository -locationInDocRepo $PathToConfigFile -monikerId $MonikerIdentifier 
      break
    }
    default {
      Write-Host "Unrecognized Language: $language"
      exit(1)
    }
  }
}
