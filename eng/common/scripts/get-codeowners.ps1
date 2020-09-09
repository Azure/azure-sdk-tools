param (
  $TargetDirectory, # should be in relative form from root of repo. EG: sdk/servicebus
  $RootDirectory, # ideally $(Build.SourcesDirectory)
  $AuthToken,
  $OwningUsers = "", # target devops output variable
  $OwningTeams = "",
  $OwningLabels = ""
)
$target = $TargetDirectory.ToLower().Trim("/")
$codeOwnersLocation = Join-Path $RootDirectory -ChildPath ".github/CODEOWNERS"
$ownedFolders = @{}

if (!(Test-Path $codeOwnersLocation)) {
  Write-Host "Unable to find CODEOWNERS file in target directory $RootDirectory"
  exit 1
}

$codeOwnersContent = Get-Content $codeOwnersLocation
$previousLine

foreach ($contentLine in $codeOwnersContent) {
  if (-not $contentLine.StartsWith("#") -and $contentLine){
    $splitLine = $contentLine -split "\s+"
    
    # CODEOWNERS file can also have labels present after the owner aliases
    # gh aliases start with @ in codeowners. don't pass on to API calls

    $aliases = ($splitLine[1..$($splitLine.Length)] | ? { $_.StartsWith("@") } | % { return $_.substring(1) }) -join ","
    $labels = ""

    if ($null -ne $previousLine -and $previousLine.StartsWith("# PRLabel:"))
    {
      $previousLine = $previousLine.Replace("# PRLabel: ","")
      $splitPrevLine = $previousLine -split "%" 
      $labels = ($splitPrevLine[1..$($splitPrevLine.Length)] | % { return $_.Trim() }) -join ","
    }

    $ownedFolders[$splitLine[0].ToLower().Trim("/")] = @{ Aliases = $aliases; Labels = $labels }
  }
  $previousLine = $contentLine
}

$results = $ownedFolders[$target]

if ($results) {
  Write-Host "Found a folder to match $target"
  $aliases = $results.Aliases -split ","
  $users
  $teams

  if ($AuthToken) {
    $headers = @{
      Authorization = "bearer $AuthToken"
    }
  }

  foreach ($str in $aliases)
  {
    $usersApiUrl = "https://api.github.com/users/$str"
    try
    {
      $response = Invoke-RestMethod -Headers $headers $usersApiUrl
      if ($users) { $users += ("," + $str) } else { $users += $str }
    }
    catch {
      if ($_.Exception.Response.StatusCode.Value__ -eq 404) # Consider it a team Alias
      {
        if ($teams) { $teams += ("," + $str) } else { $teams += $str }
      }
      else{
        LogError "Invoke-RestMethod ${usersApiUrl} failed with exception:`n$_"
        exit 1
      }
    }
  }

  if ($OwningUsers) {
    $presentOwningUsers = [System.Environment]::GetEnvironmentVariable($OwningUsers)
    if ($presentOwningUsers) { 
      $users += ",$presentOwningUsers"
    }
    Write-Host "##vso[task.setvariable variable=$OwningUsers;]$users"
  }

  if ($OwningTeams) {
    $presentOwningTeams = [System.Environment]::GetEnvironmentVariable($OwningTeams)
    if ($presentOwningTeams) { 
      $teams += ",$presentOwningTeams"
    }
    Write-Host "##vso[task.setvariable variable=$OwningTeams;]$teams"
  }

  if ($OwningLabels) {
    $presentOwningLabels = [System.Environment]::GetEnvironmentVariable($OwningLabels)
    if ($presentOwningLabels) { 
      $labels += ",$presentOwningLabels"
    }
    Write-Host "##vso[task.setvariable variable=$OwningLabels;]$($result.Labels)"
  }

  return @{ Users = $users; Teams = $teams; Labels = $results.Labels }
}
else {
  Write-Host "Unable to match path $target in CODEOWNERS file located at $codeOwnersLocation."
  Write-Host ($ownedFolders | ConvertTo-Json)
  return ""
}

