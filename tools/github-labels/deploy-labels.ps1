<#
  .SYNOPSIS
    Deploys the set of common labels to the managed set of Azure SDK repositories.

  .PARAMETER GitHubAccessToken
    The personal access token of the GitHub user who this command is being run on behalf of.

  .PARAMETER LabelFilePath
    [optional] TThe fully-qualified path (including filename) of the CSV file that contains
    the set of common label data, in GHCreator format.

  .PARAMETER RepositoryFilePath
    [optional] The fully-qualified path (including filename) of the text file that contains
    the set of repositories to update labels for, with each repository appearing on a new line.

  .PARAMETER DelayMinutes
    [optional] The number of minutes to delay between updating labels for each individual repository;
    intended to help guard against throttling.
#>

[CmdletBinding()]
param
(
  [Parameter(Mandatory=$true, HelpMessage="Please provide your GitHub access token.", Position=0)]
  [ValidateNotNullOrEmpty()]
  [string]$GitHubAccessToken,

  [Parameter(Mandatory=$false, HelpMessage="Please provide the path to the set of common labels.")]
  [ValidateScript({Test-Path $_ -PathType 'Leaf'})]
  [string]$LabelFilePath = "./common-labels.csv",

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
    Write-Host " ==================================================== " -ForegroundColor Green
    Write-Host "   Starting $($currentRepository)"                      -ForegroundColor Green
    Write-Host " ==================================================== " -ForegroundColor Green

    dotnet ./ghcreator/GHCreator.dll CreateOrUpdate $LabelFilePath $currentRepository -token $GitHubAccessToken

    Write-Host " ==================================================== " -ForegroundColor Green
    Write-Host "   $($currentRepository) complete"                      -ForegroundColor Green
    Write-Host " ==================================================== " -ForegroundColor Green

    if ($DelayMinutes -gt 0)
    {
        Write-Warning "Delaying for $($DelayMinutes) minutes..."
        Start-Sleep -Seconds ($DelayMinutes * 60)
    }
}