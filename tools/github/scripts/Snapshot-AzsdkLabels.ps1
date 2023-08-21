[CmdletBinding(DefaultParameterSetName = 'RepositoryFile', SupportsShouldProcess = $true)]
param (
    [Parameter()]
    [ValidateScript({Test-Path $_ -PathType 'Container'})]
    [string]$SnapshotDirectory = "$PSScriptRoot/../data/repository-snapshots",

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
    [int]$DelayMinutes = 1,

    [Parameter()]
    [switch] $Diff,

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

# If the output path does not exist, create it.
if (-not (Test-Path $SnapshotDirectory -PathType Container)) {
    New-Item -Path $SnapshotDirectory -ItemType Directory | Out-Null
}

# Extract the common labels and transform them into a hashset for more efficient lookups.
$commonLabels = [Collections.Generic.HashSet[string]] ((Import-Csv $LabelsFilePath -Header "Name", "Description", "Color").Name)

# Compute the non-common labels in each repository.
foreach ($repo in $Repositories) {
    if ($Force -or $PSCmdlet.ShouldProcess(
        "Preparing label snapshot for $repo",
        "Prepare label snapshot for $repo",
        "Creating a snapshot of unique repository labels")) {

        $repositoryLabels = gh label list -R $repo -L 2500 --json name

        if ($LASTEXITCODE) {
            Write-Error "Failed to query repository labels for ${repo}: $result"
        }

        $filePath = (Join-Path $SnapshotDirectory "$($repo.Substring(($repo.IndexOf('/') + 1))).txt")
        $snapshot = ((ConvertFrom-Json $repositoryLabels)| where { -not $commonLabels.Contains($_.Name) }).Name

        if ($Diff) {
            $previousSnapshot = [Collections.Generic.HashSet[string]]::new()

            if (Test-Path $filePath) {
                (Get-Content $filePath) | select { $previousSnapshot.Add($_) } | Out-Null
            }

            Write-Host "==================================================== " -ForegroundColor Green
            Write-Host "  $($repo)"                                            -ForegroundColor Green
            Write-Host "==================================================== " -ForegroundColor Green
            Write-Host "New labels added since the last snapshot:"

            $newLabels = $snapshot | where { -not $previousSnapshot.Contains($_) }

            if ($newLabels.Count -eq 0) {
                Write-Host "`t None"
            }
            else {
                $newLabels | foreach { Write-Host "`t$_" }
            }

            Write-Host ""
        }

        $snapshot | Out-File -FilePath $filePath
        Write-Verbose "Updated snapshot written to: $filePath"

        # Pause for a moment between repositories, if configured to do so.
        if (($DelayMinutes -gt 0) -and ($repo -ne $Repositories[-1])) {
            Write-Warning "Delaying for $($DelayMinutes) minutes to avoid throttling..."
            Start-Sleep -Seconds ($DelayMinutes * 60)
        }
    }
}

<#
.SYNOPSIS
Inspects labels for a set of repositories and creates a snapshot of those not part of the common set.

.DESCRIPTION
Inspects labels for a set of repositories and creates a snapshot of those labels not part of the common set.  The snapshot is written as a line-delimited list of label names in the specified directory named after the source repository.

.PARAMETER SnapshotDirectory
The fully-qualifeid path to the directory in which repository shapshot files should be written.

.PARAMETER LabelsFilePath
The fully-qualifeid path (including filename) to a CSV file of the common Azure SDK labels that will filtered from snapshots.  Columns have no headers and are in the form of "Name,Description,Color".

.PARAMETER Repositories
The GitHub repositories to inspect and build snapshots for.

.PARAMETER Languages
The Azure SDK languages whose repositories should be inspected and snapshots built for.   e.g., "net" for "Azure/azure-sdk-for-net".

.PARAMETER RepositoryFilePath
The fully-qualified path (including filename) to a new line-delmited file of respositories to inspect and build snapshots for.

.PARAMETER Force
Build snapshots for each repository without prompting.

.PARAMETER DelayMinutes
Allows a delay to be taken between repositories in order to reduce the chance of being throttle by GitHub.  Because labels are read in a single request, chances of throttling are low, but this parameter is provided as a precaution.

.EXAMPLE
Snapshot-AzsdkLabels.ps1 -WhatIf
See which repositories will have label snapshots taken.

.EXAMPLE
Snapshot-AzsdkLabels.ps1 -SnapshotDirectory "../data/repository-snapshots" -LabelsFilePath "../data/common-labels.csv" -RepositoryFilePath "../data/repositories.txt" -DelayMinutes 2 -Diff
Prepares snapshots of the non-common labels of the repositories listed in the file "../data/repositories.txt" and prints the diff since the last snapshot.  A 2 minute delay between repositories is used.
#>
