param (
  $TargetDirectory, # should be in relative form from root of repo. EG: sdk/servicebus
  $RootDirectory # ideally $(Build.SourcesDirectory)
)

$codeOwnersLocation = @(Get-ChildItem -R -Path $RootDirectory -Filter "CODEOWNERS")

if ($codeOwnersLocation.Length -eq 0) {
  Write-Host "Unable to find CODEOWNERS file in target directory $RootDirectory"
  exit(1)
}

$codeOwnersContent = Get-Content ($codeOwnersLocation | Select-Object -Last 1)

$ownedFolders = @{}

foreach ($contentLine in $codeOwnersContent) {
  if (-not $contentLine.StartsWith("#") -and -not $contentLine.IsNullOrWhitespace){
    Write-Host ($contentLine -split "\s")
    Write-Host $contentLine
  }
}