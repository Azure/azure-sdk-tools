[CmdletBinding(DefaultParameterSetName = 'RepositoryFile', SupportsShouldProcess = $true)]
param (
    [Parameter()]
    [ValidateScript({Test-Path $_ -PathType 'Leaf'})]
    [string] $LabelsFilePath = "$PSScriptRoot/../data/common-labels.csv",

    [Parameter(ParameterSetName = 'RepositoryFile')]
    [ValidateScript({Test-Path $_ -PathType 'Leaf'})]
    [string] $RepositoryFilePath = "$PSScriptRoot/../data/repositories.txt",

    [Parameter(ParameterSetName = 'Repositories')]
    [ValidateNotNullOrEmpty()]
    [string[]] $Repositories = @(
        'Azure/azure-sdk-for-cpp'
        'Azure/azure-sdk-for-go'
        'Azure/azure-sdk-for-java'
        'Azure/azure-sdk-for-js'
        'Azure/azure-sdk-for-net'
        'Azure/azure-sdk-for-python'
        'Azure/azure-sdk-tools'
    ),

    [Parameter(ParameterSetName = 'Languages')]
    [ValidateNotNullOrEmpty()]
    [string[]] $Languages = @('cpp', 'go', 'java', 'js', 'net', 'python', 'c', 'ios', 'android'),

    [Parameter()]
    [ValidateRange(0,100)]
    [int]$DelayMinutes = 2,

    [Parameter()]
    [switch] $Force
)

if (!(Get-Command -Type Application gh -ErrorAction Ignore)) {
    throw 'You must first install the GitHub CLI: https://github.com/cli/cli/tree/trunk#installation'
}

if ($PSCmdlet.ParameterSetName -eq 'Languages') {
    $Repositories = foreach ($lang in $Languages) {
        "Azure/azure-sdk-for-$lang"
    }
}

if ($PSCmdlet.ParameterSetName -eq 'RepositoryFile') {
    $Repositories = Get-Content $RepositoryFilePath
}

# Extract the label content to be sync'd.
$labels = Import-Csv $LabelsFilePath -Header "Name", "Description", "Color"

# Synchronize labels for each repository.
$activity = "Synchronizing labels"
Write-Progress -Activity $activity -PercentComplete 0

$totalLabels = $labels.Count * $Repositories.Count
$labelCount = 0

foreach ($repo in $Repositories) {
    if ($Force -or $PSCmdlet.ShouldProcess(
        "Writing common labels to $repo",
        "Write common labels to $repo",
        "Create or update common labels")) {

        foreach ($label in $labels) {
            $result = gh label create $label.Name -d $label.Description -c $label.Color -R "$repo" --force 2>&1

            if ($LASTEXITCODE) {
                Write-Error "Failed to create or update the common labels to ${repo}: $result"
            }

            $labelCount++
            $completed = ($labelCount / $totalLabels) * 100
            Write-Progress -Activity $activity -Status "$($repo): $($label.Name)" -PercentComplete $completed
        }

        # Pause for a moment between repositories, if configured to do so.
        if (($DelayMinutes -gt 0) -and ($repo -ne $Repositories[-1])) {
            Write-Progress -Activity $activity -Status "$($repo): Complete.  Delaying to avoid throttling."
            Write-Warning "Delaying for $($DelayMinutes) minutes to avoid throttling..."
            Start-Sleep -Seconds ($DelayMinutes * 60)
        }
    }
}

Write-Progress -Activity $activity -Completed

<#
.SYNOPSIS
Synchonizes the common set of Azure SDK labels to one or more repository.

.DESCRIPTION
Creates or updates labels - without deleting any - ensuring the common Azure SDK label set exists in all listed repositories.

.PARAMETER LabelsFilePath
The fully-qualifeid path (including filename) to a CSV file of the common Azure SDK labels that will be created or updated in each repository.  Columns have no headers and are in the form of "Name,Description,Color".

.PARAMETER Repositories
The GitHub repositories to update with the common label set.

.PARAMETER Languages
The Azure SDK languages whose repositories should be updated with the common label set.  e.g., "net" for "Azure/azure-sdk-for-net".

.PARAMETER RepositoryFilePath
The fully-qualified path (including filename) to a new line-delmited file of respositories to update with the common label set.

.PARAMETER Force
Synchronize common labels for each repository without prompting.

.PARAMETER DelayMinutes
Allows a delay to be taken between repositories in order to reduce the chance of being throttle by GitHub.  Because labels must be pushed one-by-one, a large number of GitHub operations is made for each repository.

.EXAMPLE
Sync-AzsdkLabels.ps1 -WhatIf
See which repositories will synchronized labels.

.EXAMPLE
Sync-AzsdkLabels.ps1 -LabelsFilePath "../data/common-labels.csv" -RepositoryFilePath "../data/repositories.txt" -DelayMinutes 2
Synchronize the common labels to the repositories listed in the file "../data/repositories.txt" with a 2 minute delay between each repository.
#>
