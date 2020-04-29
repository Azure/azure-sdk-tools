# Note, due to how `Expand-Archive` is leveraged in this script,
# powershell core is a requirement for successful execution.
param (
  $ArtifactLocation, # the root of the artifact folder. DevOps $(System.ArtifactsDirectory)
  $WorkDirectory, # a clean folder that we can work in
  $Repository, # EG: "Maven", "PyPI", "NPM"
  $ReleaseSHA, # the SHA for the artifacts. DevOps: $(Release.Artifacts.<artifactAlias>.SourceVersion) or $(Build.SourceVersion)
  $RepoId, # full repo id. EG azure/azure-sdk-for-net  DevOps: $(Build.Repository.Id)
  $TargetDocRepo, # the repository that we will be writing the readmes to 
  $LatestBranch = "smoke-test", # normally `smoke-test`
  $PreviewBranch = "smoke-test-preview", # normally `smoke-test-preview`
  $DocRepoContentLocation = "docs-ref-services/" # within the doc repo, where does our readme go?
)

Write-Host (Join-Path $PSScriptRoot package-parsing.ps1)
. (Join-Path $PSScriptRoot package-parsing.ps1)

$date = Get-Date -Format "MM/dd/yyyy"
$apiUrl = "https://api.github.com/repos/$repoId"

$pkgs = VerifyPackages -pkgRepository $Repository -artifactLocation $ArtifactLocation -workingDirectory $WorkDirectory -apiUrl $apiUrl -releaseSha $ReleaseSHA -exitOnError $False

if ($pkgList) {
  Write-Host "Given the visible artifacts, readmes will be copied for the following packages"

  foreach ($packageInfo in $pkgList) {
    Write-Host $packageInfo.PackageId
    Write-Host $packageInfo.ReadmeContent
  }
}
else {
  Write-Host "No readmes discovered for doc release under folder $ArtifactLocation."
}
