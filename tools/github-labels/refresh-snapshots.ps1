<#
  .SYNOPSIS
    Captures a snapshot of the current set of labels in the managed set of Azure SDK repositories.

  .PARAMETER GitHubAccessToken
    The personal access token of the GitHub user who this command is being run on behalf of.

  .PARAMETER SnapshotDirectory
    [optional] The directory to which snapshots should be written.

  .PARAMETER RepositoryFilePath
    [optional] The fully-qualified path (including filename) of the text file that contains
    the set of repositories to query, with each repository appearing on a new line.

  .PARAMETER DelayMinutes
    [optional] The number of minutes to delay between querying each individual repository; 
    intended to help guard against throttling.
#>

[CmdletBinding()]
param
( 
  [Parameter(Mandatory=$true, HelpMessage="Please provide your GitHub access token.", Position=0)]
  [string] $GitHubAccessToken,

  [Parameter(Mandatory=$false, HelpMessage="Please provide the directory to write snapshots to.")]
  [ValidateScript({Test-Path $_ -PathType 'Container'})] 
  [string]$SnapshotDirectory = "./repository-snapshots",

  [Parameter(Mandatory=$false, HelpMessage="Please provide the path to the set of repositories.")]
  [ValidateScript({Test-Path $_ -PathType 'Leaf'})] 
  [string]$RepositoryFilePath = "./repositories.txt",

  [Parameter(Mandatory=$false, HelpMessage="Please provide the time to delay between repositories (in minutes), between 0 and 100.")]
  [ValidateRange(0,100)]
  [int]$DelayMinutes = 0
)

$repositories = Get-Content $RepositoryFilePath

foreach ($currentRepository in $repositories)
{
    $name = $currentRepository.Replace("Azure\", "")
    
    Write-Host " ==================================================== " -ForegroundColor Green
    Write-Host " Starting $($currentRepository)"                        -ForegroundColor Green    

    (dotnet ./ghcreator/GHCreator.dll List Label $currentRepository -token $GitHubAccessToken) | Out-File -FilePath (Join-Path $SnapshotDirectory "$($name).csv")

    Write-Host " Complete"                                              -ForegroundColor Green
    Write-Host " ==================================================== " -ForegroundColor Green

    if ($DelayMinutes -gt 0)
    {
        Write-Warning "Delaying for $($DelayMinutes) minutes..."
        Start-Sleep -Seconds ($DelayMinutes * 60)
    }
}