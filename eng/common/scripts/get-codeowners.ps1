param (
  $TargetDirectory, # should be in relative form from root of repo. EG: sdk/servicebus
  $RootDirectory # ideally $(Build.SourcesDirectory)
)

$codeOwnersLocation = @(Get-ChildItem -R -Force -Path $RootDirectory -Filter "CODEOWNERS")

if ($codeOwnersLocation.Length -eq 0) {
  Write-Host "Unable to find CODEOWNERS file in target directory $RootDirectory"
  exit 1
}

if ($codeOwnersLocation.Length -gt 1) {
  Write-Host "Multiple CODEOWNERS files detected in  $RootDirectory."
  Write-Host "$codeOwnersLocation"
  exit 1
}


$codeOwnersContent = Get-Content ($codeOwnersLocation | Select-Object -Last 1)

$ownedFolders = @{}

foreach ($contentLine in $codeOwnersContent) {
  if (-not $contentLine.StartsWith("#") -and $contentLine){
    $splitLine = $contentLine -split "\s" | ? { return $_ }
    
    # in the codeowners file, gh aliases start with @. we don't want that when passing them to the API
    $ownedFolders[$splitLine[0].ToLower()] = ($splitLine[1..$($splitLine.Length)] | % { return $_.substring(1) }) -join ","
  }
}

$results = $ownedFolders[$TargetDirectory.ToLower()]

if ($results) {
  Write-Host "Discovered code owners for path $TargetDirectory are $results."
  return $results -join ","
}
else {
  Write-Host "Unable to match path $TargetDirectory in CODEOWNERS file located at $codeOwnersLocation."
  return ""
}

