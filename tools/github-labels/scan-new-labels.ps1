<#
  .SYNOPSIS
    Scans the labels for the set managed Azure SDK repositories and compares them to the 
    last snapshot captured; new labels are reported to host output.

  .PARAMETER GitHubAccessToken
    The personal access token of the GitHub user who this command is being run on behalf of.

  .PARAMETER SnapshotDirectory
    [optional] The directory from which snapshots will be read.  The snapshots are assumed to
    be named << repository >>.csv, where the `repository` has the `Azure\` prefix removed.

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

  [Parameter(Mandatory=$false, HelpMessage="Please provide the directory read snapshots from.")]
  [ValidateScript({Test-Path $_ -PathType 'Container'})] 
  [string]$SnapshotDirectory = "./repository-snapshots",

  [Parameter(Mandatory=$false, HelpMessage="Please provide the path to the set of repositories.")]
  [ValidateScript({Test-Path $_ -PathType 'Leaf'})] 
  [string]$RepositoryFilePath = "./repositories.txt",

  [Parameter(Mandatory=$false, HelpMessage="Please provide the time to delay between repositories (in minutes), between 0 and 100.")]
  [ValidateRange(0,100)]
  [int]$DelayMinutes = 0
)

function BuildSnapshotHash($snapshotFilePath)
{
    $labels = [System.Collections.Generic.HashSet[string]]::new()

    foreach ($line in (Get-Content $snapshotFilePath) | Select-Object -Skip 1)
    {
        if (-not [String]::IsNullOrEmpty($line))
        {
            [string]$label = (($line -split ",")[1])
            $labels.Add($label) | Out-Null
        }
    }

    return $labels
}

# ====================
# == Script Actions ==
# ====================

$repositories = Get-Content $RepositoryFilePath

foreach ($currentRepository in $repositories)
{
    $name = $currentRepository.Replace("Azure\", "")
    
    Write-Host " ==================================================== " -ForegroundColor Green
    Write-Host " $($currentRepository)"                                 -ForegroundColor Green    
    Write-Host " ==================================================== " -ForegroundColor Green

    $snapshotLabels = BuildSnapshotHash (Join-Path $SnapshotDirectory "$($name).csv")
    $currentLabels = ((dotnet ./ghcreator/GHCreator.dll List Label $currentRepository -token $GitHubAccessToken) | Select-Object -Skip 1)
    $any = $false

    foreach ($line in $currentLabels)
    {
        if (-not [String]::IsNullOrEmpty($line))
        {
            [string]$label = (($line -split ",")[1])
            
            if (-not $snapshotLabels.Contains($label))
            {
                Write-Host "`t$($label)"
                $any = $true
            }
        }
    }

    if (-not $any)
    {
        Write-Host "`tNo new labels found"
    }
    
    Write-Host ""
    Write-Host ""

    if ($DelayMinutes -gt 0)
    {
        Write-Warning "Delaying for $($DelayMinutes) minutes..."
        Start-Sleep -Seconds ($DelayMinutes * 60)
    }
}