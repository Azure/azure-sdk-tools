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
  
  [ValidateSet("Nuget","NPM","PyPI","Maven")]
  $Repository, # EG: "Maven", "PyPI", "NPM"

  # Used for All Languages
  [Parameter(Mandatory = $true)]
  $ConfigurationRepository,

  [Parameter(Mandatory = $true)]
  $ConfigurationAccessPAT,

  # arguments specific to each language follow

  # Java
  $MonikerIdentifier

  # C# 
  $PathToConfigFile,
  
  # JS and Python only
  $ApiUrl,
  $BuildId,
  $TargetVariable

)

$apiUrl = "https://api.github.com/repos/$repoId"
$pkgs = VerifyPackages -pkgRepository $Repository `
  -artifactLocation $ArtifactLocation `
  -workingDirectory $WorkDirectory `
  -apiUrl $apiUrl `
  -releaseSha $ReleaseSHA `
  -continueOnError $True

if ($pkgs) {
  Write-Host "Given the visible artifacts, the following package verions will be set."
  Write-Host ($pkgs | % { $_.PackageId + " " + $_.PackageVersion })

  foreach ($packageInfo in $pkgs) {
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
        break
      }
      default {
        Write-Host "Unrecognized Language: $language"
        exit(1)
      }
    }
  }
}
