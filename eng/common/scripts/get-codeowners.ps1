param (
  $TargetDirectory, # should be in relative form from root of repo. EG: sdk/servicebus
  $RootDirectory, # ideally $(Build.SourcesDirectory)
  $AuthToken,
  $VsoOwningUsers = "", # target devops output variable
  $VsoOwningTeams = "",
  $VsoOwningLabels = ""
)
$target = $TargetDirectory.ToLower().Trim("/")
$codeOwnersLocation = Join-Path $RootDirectory -ChildPath ".github/CODEOWNERS"
$ownedFolders = @{}

if (!(Test-Path $codeOwnersLocation)) {
  Write-Host "Unable to find CODEOWNERS file in target directory $RootDirectory"
  exit 1
}

$codeOwnersContent = Get-Content $codeOwnersLocation

function VerifyAlias($APIUrl)
{
  if ($AuthToken) {
    $headers = @{
      Authorization = "bearer $AuthToken"
    }
  }
  try
  {
    $response = Invoke-RestMethod -Headers $headers $APIUrl
  }
  catch 
  {
    Write-Host "Invoke-RestMethod ${APIUrl} failed with exception:`n$_"
    Write-Host "This might be because a team alias was used for user API request or vice versa."
    return $false
  }
  return $true
}

foreach ($contentLine in $codeOwnersContent) {
  if (-not $contentLine.StartsWith("#") -and $contentLine){
    $splitLine = $contentLine -split "\s+"
    
    # CODEOWNERS file can also have labels present after the owner aliases
    # gh aliases start with @ in codeowners. don't pass on to API calls

    $aliases = ($splitLine[1..$($splitLine.Length)] | ? { $_.StartsWith("@") } | % { return $_.substring(1) }) -join ","
    $labels = ""

    if ($null -ne $previousLine -and $previousLine.Contains("PRLabel:"))
    {
      $previousLine = $previousLine.substring($previousLine.IndexOf(':') + 1)
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

  foreach ($str in $aliases)
  {
    $usersApiUrl = "https://api.github.com/users/$str"
    if (VerifyAlias -APIUrl $usersApiUrl)
    {
      if ($users) { $users += ("," + $str) } else { $users += $str }
    }
    else {
      if ($str.IndexOf('/') -ne -1) # Check if it's a team alias e.g. Azure/azure-sdk-eng
      {
        $org = $str.substring(0, $str.IndexOf('/'))
        $team_slug = $str.substring($str.IndexOf('/') + 1)
        $teamApiUrl =  "https://api.github.com/orgs/$org/teams/$team_slug"
        if (VerifyAlias -APIUrl $teamApiUrl)
        {
          if ($teams) { $teams += ("," + $str) } else { $teams += $str }
          continue
        }
      }
      Write-Host "Alias ${str} is neither a recognized github user nor a team"
    }
  }

  if ($VsoOwningUsers) {
    $presentOwningUsers = [System.Environment]::GetEnvironmentVariable($VsoOwningUsers)
    if ($presentOwningUsers) { 
      $users += ",$presentOwningUsers"
    }
    Write-Host "##vso[task.setvariable variable=$VsoOwningUsers;]$users"
  }

  if ($VsoOwningTeams) {
    $presentOwningTeams = [System.Environment]::GetEnvironmentVariable($VsoOwningTeams)
    if ($presentOwningTeams) { 
      $teams += ",$presentOwningTeams"
    }
    Write-Host "##vso[task.setvariable variable=$VsoOwningTeams;]$teams"
  }

  if ($VsoOwningLabels) {
    $presentOwningLabels = [System.Environment]::GetEnvironmentVariable($VsoOwningLabels)
    if ($presentOwningLabels) { 
      $labels += ",$presentOwningLabels"
    }
    Write-Host "##vso[task.setvariable variable=$VsoOwningLabels;]$($result.Labels)"
  }

  return @{ Users = $users; Teams = $teams; Labels = $results.Labels }
}
else {
  Write-Host "Unable to match path $target in CODEOWNERS file located at $codeOwnersLocation."
  Write-Host ($ownedFolders | ConvertTo-Json)
  return ""
}

