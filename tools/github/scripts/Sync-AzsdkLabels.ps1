[CmdletBinding(DefaultParameterSetName = 'RepositoryFile', SupportsShouldProcess = $true)]
param (
    [Parameter(Position = 0, Mandatory = $true)]
    [ValidatePattern('^\w+/[\w-]+$')]
    [string] $SourceRepository,

    [Parameter(ParameterSetName = 'RepositoryFile')]
    [ValidateScript({Test-Path $_ -PathType 'Leaf'})]
    [string] $RepositoryFilePath = "$PSScriptRoot/../repositories.txt",

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
    [string[]] $Languages = @('cpp', 'go', 'java', 'js', 'net', 'python'),

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

# Filter out the source repository.
$Repositories = $Repositories.Where({$_ -ne $SourceRepository})

foreach ($repo in $Repositories) {
    if ($Force -or $PSCmdlet.ShouldProcess(
        "Cloning labels from $SourceRepository to $repo",
        "Clone labels from $SourceRepository to $repo?",
        "Clone labels")) {
        $result = gh -R "$repo" label clone "$SourceRepository" --force 2>&1
        if ($LASTEXITCODE) {
            Write-Error "Failed to clone labels from $SourceRepository to ${repo}: $result"
        }
    }
}

<#
.SYNOPSIS
Clones labels from a source repository to all other repositories.

.DESCRIPTION
Clones labels - without deleting any - from a source repository to all other listed repositories.

.PARAMETER Repositories
The GitHub repositories to update.

.PARAMETER Languages
The Azure SDK languages to query for milestones e.g., "net" for "Azure/azure-sdk-for-net".

.PARAMETER RepositoryFilePath
The fully-qualified path (including filename) to a new line-delmited file of respositories to update.

.PARAMETER Force
Create milestones for each repository without prompting.

.EXAMPLE
Sync-AzsdkLabels.ps1 -WhatIf
See which repositories will receive cloned labels.
#>
