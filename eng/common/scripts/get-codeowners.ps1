param (
  $TargetDirectory, # should be in relative form from root of repo. EG: sdk/servicebus
  $RootDirectory # ideally $(Build.SourcesDirectory)
)

function Get-Longest-Match($path, $codeOwnersIndex) {
  $matchedLength = 0
  $matchedPath = "" 

  foreach($key in $codeOwnersIndex.keys) {
    $keyLength = $key.Length
    if ($path.startsWith($key) -and $keyLength -ge $matchedLength) {
      $matchedLength = $keyLength
      $matchedPath = $key
    }
  }

  return $matchedPath
}

$target = $TargetDirectory.ToLower()
$codeOwnersLocation = Join-Path $RootDirectory -ChildPath ".github/CODEOWNERS"
$ownedFolders = @{}

if (!(Test-Path $codeOwnersLocation)) {
  Write-Host "Unable to find CODEOWNERS file in target directory $RootDirectory"
  exit 1
}

$codeOwnersContent = Get-Content $codeOwnersLocation

foreach ($contentLine in $codeOwnersContent) {
  if (-not $contentLine.StartsWith("#") -and $contentLine){
    $splitLine = $contentLine -split "\s+"
    
    # CODEOWNERS file can also have labels present after the owner aliases
    # gh aliases start with @ in codeowners. don't pass on to API calls
    $ownedFolders[$splitLine[0].ToLower()] = ($splitLine[1..$($splitLine.Length)] `
      | ? { $_.StartsWith("@") } `
      | % { return $_.substring(1) }) -join ","
  }
}

$results = $ownedFolders[$target]
$looseMatch = Get-Longest-Match -path $target -codeOwnersIndex $ownedFolders

if ($looseMatch) {
  Write-Host "Found a folder $looseMatch to match $target"

  return $ownedFolders[$looseMatch]
}
else {
  Write-Host "Unable to match path $target in CODEOWNERS file located at $codeOwnersLocation."
  Write-Host ($ownedFolders | ConvertTo-Json)
  return ""
}

