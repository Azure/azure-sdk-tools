<#
  .SYNOPSIS
    Compares the labels for an Azure SDK repository against the set of labels common
    to all repositories, outputting labels that exist only for the repository.

  .PARAMETER GitHubAccessToken
    The personal access token of the GitHub user who this command is being run on behalf of.

  .PARAMETER RepositoryName
    The full name (inlcuding owner organization) of the repository that is the
    source for the labels being compared to the common set.  For example, "Azure\azure-sdk-for-net".

  .PARAMETER CommonLabelFilePath
    [optional] The fully-qualified path (including filename) of the .csv file that contains
    the set of labels common to all repositories, in GHChreate format.
#>

[CmdletBinding()]
param
(
  [Parameter(Mandatory=$true, HelpMessage="Please provide your GitHub access token.", Position=0)]
  [ValidateNotNullOrEmpty()]
  [string]$GitHubAccessToken,

  [Parameter(Mandatory=$true, HelpMessage="Please provide the full name of the repository (including organization) to read labels from.", Position=1)]
  [ValidateNotNullOrEmpty()]
  [string]$RepositoryName,

  [Parameter(Mandatory=$false, HelpMessage="Please provide the path to the set of common labels, in GHCreate format.")]
  [ValidateScript({Test-Path $_ -PathType 'Leaf'})]
  [string]$CommonLabelFilePath = "./common-labels.csv"
)

function BuildCommonLabelHash($commonLabelFilePath)
{
    $labels = [System.Collections.Generic.HashSet[string]]::new()

    foreach ($line in (Get-Content $commonLabelFilePath))
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

Write-Host " ==================================================== " -ForegroundColor Green
Write-Host "  $($RepositoryName)"                                   -ForegroundColor Green
Write-Host " ==================================================== " -ForegroundColor Green

$commonLabels = (BuildCommonLabelHash $CommonLabelFilePath)
$repositoryLabels = ((dotnet ./ghcreator/GHCreator.dll List Label $RepositoryName -token $GitHubAccessToken) | Select-Object -Skip 1)
$any = $false

foreach ($line in $repositoryLabels)
{
    if (-not [String]::IsNullOrEmpty($line))
    {
        [string]$label = (($line -split ",")[1])

        if (-not $commonLabels.Contains($label))
        {
            Write-Host "`t$($label)"
            $any = $true
        }
    }
}

if (-not $any)
{
    Write-Host "`tThe repository contains no labels not in the common set."
}

Write-Host ""
Write-Host ""