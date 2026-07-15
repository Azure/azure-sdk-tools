[CmdletBinding(DefaultParameterSetName = 'RepositoryFile', SupportsShouldProcess = $true)]
param (
    [Parameter()]
    [ValidateScript({Test-Path $_ -PathType 'Leaf'})]
    [string] $LabelsFilePath = "$PSScriptRoot/../data/common-labels.csv",

    [Parameter(ParameterSetName = 'RepositoryFile')]
    [ValidateScript({Test-Path $_ -PathType 'Leaf'})]
    [string] $RepositoryFilePath = "$PSScriptRoot/../data/repositories.txt",

    [Parameter(ParameterSetName = 'Repositories', Mandatory = $true)]
    [string[]] $Repositories,

    [Parameter(ParameterSetName = 'Languages', Mandatory = $true)]
    [string[]] $Languages,

    [Parameter()]
    [ValidateRange(0,100)]
    [int]$DelayMinutes = 2,

    [Parameter()]
    [switch] $Incremental,

    [Parameter()]
    [switch] $Force
)

function ConvertFrom-LabelCsvContent([string]$Content) {
    if ([string]::IsNullOrWhiteSpace($Content)) {
        return @()
    }

    $rows = @(
        ($Content -split "`r?`n") |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            ConvertFrom-Csv -Header "Name", "Description", "Color"
    )

    return @(
        foreach ($row in $rows) {
            [PSCustomObject]@{
                Name        = [string]$row.Name
                Description = if ($null -eq $row.Description) { '' } else { [string]$row.Description }
                Color       = if ($null -eq $row.Color) { '' } else { [string]$row.Color }
            }
        }
    )
}

function Get-LabelDefinitionsFromFile([string]$Path) {
    return @(ConvertFrom-LabelCsvContent -Content (Get-Content -Raw $Path))
}

function Normalize-LabelDefinition([object]$Label) {
    return [PSCustomObject]@{
        Name        = [string]$Label.Name
        Description = if ($null -eq $Label.Description) { '' } else { [string]$Label.Description }
        Color       = if ($null -eq $Label.Color) { '' } else { ([string]$Label.Color).ToLowerInvariant() }
    }
}

function ConvertTo-LabelLookup([object[]]$Labels) {
    $lookup = @{}

    foreach ($label in $Labels) {
        $normalized = Normalize-LabelDefinition -Label $label
        $lookup[$normalized.Name] = $normalized
    }

    return $lookup
}

function Test-LabelDefinitionMatches([object]$Left, [object]$Right) {
    return ($Left.Description -eq $Right.Description) -and ($Left.Color -eq $Right.Color)
}

function Get-GitRelativePath([string]$RepositoryRoot, [string]$Path) {
    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    $resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    return [System.IO.Path]::GetRelativePath($resolvedRoot, $resolvedPath)
}

function Get-GitFileContent([string]$RepositoryRoot, [string]$RelativePath, [string]$Revision) {
    $gitPath = $RelativePath -replace '\\', '/'
    $content = @(git -C $RepositoryRoot show "${Revision}:$gitPath" 2>$null)

    if ($LASTEXITCODE) {
        return $null
    }

    return ($content -join "`n")
}

function Get-IncrementalLabelDefinitions([string]$Path) {
    $pathDirectory = Split-Path -Parent (Resolve-Path $Path).Path
    $repositoryRoot = @(git -C $pathDirectory rev-parse --show-toplevel 2>&1)

    if ($LASTEXITCODE -or $repositoryRoot.Count -eq 0) {
        throw "Failed to determine the git repository for '$Path': $($repositoryRoot -join [Environment]::NewLine)"
    }

    $repositoryRoot = $repositoryRoot[-1].Trim()
    $relativePath = Get-GitRelativePath -RepositoryRoot $repositoryRoot -Path $Path
    $status = @(git -C $repositoryRoot status --porcelain -- $relativePath 2>&1)

    if ($LASTEXITCODE) {
        throw "Failed to inspect git status for '$Path': $($status -join [Environment]::NewLine)"
    }

    $beforeContent = ''
    $afterContent = ''

    if ($status.Count -gt 0) {
        $headContent = Get-GitFileContent -RepositoryRoot $repositoryRoot -RelativePath $relativePath -Revision 'HEAD'

        if ($null -ne $headContent) {
            $beforeContent = $headContent
        }

        $afterContent = Get-Content -Raw $Path
    }
    else {
        $lastCommit = @(git -C $repositoryRoot log -n 1 --format=%H -- $relativePath 2>&1)

        if ($LASTEXITCODE -or $lastCommit.Count -eq 0) {
            throw "Failed to determine the last commit that changed '$Path': $($lastCommit -join [Environment]::NewLine)"
        }

        $revision = $lastCommit[-1].Trim()
        $previousContent = Get-GitFileContent -RepositoryRoot $repositoryRoot -RelativePath $relativePath -Revision "$revision^"

        if ($null -ne $previousContent) {
            $beforeContent = $previousContent
        }

        $committedContent = Get-GitFileContent -RepositoryRoot $repositoryRoot -RelativePath $relativePath -Revision $revision

        if ($null -eq $committedContent) {
            throw "Failed to read '$Path' from commit '$revision'."
        }

        $afterContent = $committedContent
    }

    $beforeLookup = ConvertTo-LabelLookup -Labels (ConvertFrom-LabelCsvContent -Content $beforeContent)
    $afterLookup = ConvertTo-LabelLookup -Labels (ConvertFrom-LabelCsvContent -Content $afterContent)
    $changedLabels = @()

    foreach ($labelName in $afterLookup.Keys) {
        if (-not $beforeLookup.ContainsKey($labelName) -or -not (Test-LabelDefinitionMatches -Left $beforeLookup[$labelName] -Right $afterLookup[$labelName])) {
            $changedLabels += $afterLookup[$labelName]
        }
    }

    return $changedLabels
}

function Get-RepositoryLabelLookup([string]$Repository) {
    $repositoryLabels = gh label list -R $Repository -L 2500 --json name,description,color 2>&1

    if ($LASTEXITCODE) {
        throw "Failed to query repository labels for ${Repository}: $repositoryLabels"
    }

    return ConvertTo-LabelLookup -Labels (ConvertFrom-Json $repositoryLabels)
}

function Test-IncrementalSyncNeeded([string]$Repository, [object[]]$ChangedLabels, [string]$Path) {
    if ($ChangedLabels.Count -eq 0) {
        Write-Host "Skipping $Repository because no labels were added or updated in '$Path'."
        return $false
    }

    $repositoryLabels = Get-RepositoryLabelLookup -Repository $Repository

    foreach ($label in $ChangedLabels) {
        if (-not $repositoryLabels.ContainsKey($label.Name)) {
            Write-Host "Repository $Repository requires incremental sync because label '$($label.Name)' must be added."
            return $true
        }

        if (-not (Test-LabelDefinitionMatches -Left $repositoryLabels[$label.Name] -Right $label)) {
            Write-Host "Repository $Repository requires incremental sync because label '$($label.Name)' must be updated."
            return $true
        }
    }

    Write-Host "Skipping $Repository because incremental label changes are already applied."
    return $false
}

if (!(Get-Command -Type Application gh -ErrorAction Ignore)) {
    throw 'You must first install the GitHub CLI: https://github.com/cli/cli/tree/trunk#installation'
}

if ($Incremental -and !(Get-Command -Type Application git -ErrorAction Ignore)) {
    throw 'You must first install git to use the Incremental switch.'
}

if ($PSCmdlet.ParameterSetName -eq 'Languages') {
    $Repositories = foreach ($lang in $Languages) {
        "Azure/azure-sdk-for-$lang"
    }
}

if ($PSCmdlet.ParameterSetName -eq 'RepositoryFile') {
    $Repositories = @(
        Get-Content $RepositoryFilePath |
            ForEach-Object { ([string]$_).Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and -not $_.EndsWith('-pr', [StringComparison]::OrdinalIgnoreCase) }
    )
}

# Extract the label content to be sync'd.
$labels = Get-LabelDefinitionsFromFile -Path $LabelsFilePath
$changedLabels = @()

if ($Incremental) {
    $changedLabels = @(Get-IncrementalLabelDefinitions -Path $LabelsFilePath)
}

# Synchronize labels for each repository.
$activity = "Synchronizing labels"
Write-Progress -Activity $activity -PercentComplete 0

$totalLabels = $labels.Count * $Repositories.Count
$labelCount = 0

foreach ($repo in $Repositories) {
    if ($Incremental -and -not (Test-IncrementalSyncNeeded -Repository $repo -ChangedLabels $changedLabels -Path $LabelsFilePath)) {
        $totalLabels -= $labels.Count
        continue
    }

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
Creates or updates labels - without deleting any - ensuring the common Azure SDK label set exists in all listed repositories. When repositories are loaded from RepositoryFilePath, entries ending in "-pr" are skipped before any synchronization, progress reporting, or delay calculations occur.

.PARAMETER LabelsFilePath
The fully-qualifeid path (including filename) to a CSV file of the common Azure SDK labels that will be created or updated in each repository.  Columns have no headers and are in the form of "Name,Description,Color".

.PARAMETER Repositories
The GitHub repositories to update with the common label set.

.PARAMETER Languages
The Azure SDK languages whose repositories should be updated with the common label set.  e.g., "net" for "Azure/azure-sdk-for-net".

.PARAMETER RepositoryFilePath
The fully-qualified path (including filename) to a new line-delmited file of respositories to update with the common label set. Entries ending in "-pr" are skipped.

.PARAMETER Incremental
Determines whether each repository should be synchronized based only on labels added or updated in the labels CSV. When local staged or unstaged changes exist for the labels file, those pending changes are evaluated; otherwise, the last commit that changed the file is evaluated.

.PARAMETER Force
Synchronize common labels for each repository without prompting.

.PARAMETER DelayMinutes
Allows a delay to be taken between repositories in order to reduce the chance of being throttle by GitHub.  Because labels must be pushed one-by-one, a large number of GitHub operations is made for each repository.

.EXAMPLE
Sync-AzsdkLabels.ps1 -WhatIf
See which repositories will synchronized labels.

.EXAMPLE
Sync-AzsdkLabels.ps1 -Incremental -WhatIf
See which repositories would be synchronized based on added or updated labels in the labels file.

.EXAMPLE
Sync-AzsdkLabels.ps1 -LabelsFilePath "../data/common-labels.csv" -RepositoryFilePath "../data/repositories.txt" -DelayMinutes 2
Synchronize the common labels to the repositories listed in the file "../data/repositories.txt" with a 2 minute delay between each repository.
#>
