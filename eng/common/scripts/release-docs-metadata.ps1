# Note, due to how `Expand-Archive` is leveraged in this script,
# powershell core is a requirement for successful execution.
param (
  $ArtifactLocation, # the root of the artifact folder. DevOps $(System.ArtifactsDirectory)
  $WorkDirectory, # a clean folder that we can work in
  $Language, #
  $TargetDocRepo, # the repository 
  $LatestBranch,
  $ 
  $RepoId, # full repo id. EG azure/azure-sdk-for-net  DevOps: $(Build.Repository.Id),
)

. $PSScriptRoot/package-parsing.ps1

$date = Get-Date -Format "MM/dd/yyyy"
$apiUrl = "https://api.github.com/repos/$repoId"


$pkgs = VerifyPackages -pkgRepository $packageRepository -artifactLocation $ArtifactLocation -workingDirectory $WorkDirectory -apiUrl $apiUrl -releaseSha $releaseSha -exitOnError $False

if ($pkgList) {
  Write-Host "Given the visible artifacts, readmes will be copied for the following packages"

  foreach ($packageInfo in $pkgList) {
    Write-Host $packageInfo.PackageId
  }
}
else {
  Write-Host "No readmes discovered for doc release under folder $ArtifactLocation."
}
