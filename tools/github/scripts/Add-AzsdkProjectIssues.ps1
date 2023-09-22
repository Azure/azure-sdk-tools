[CmdletBinding(DefaultParameterSetName = 'Repositories', SupportsShouldProcess = $true)]
param (
    [Parameter(Mandatory = $true, Position = 0)]
    [int] $ProjectNumber,

    [Parameter(Mandatory = $true, Position = 1)]
    [string[]] $Labels,

    [Parameter(ParameterSetName = 'Repositories')]
    [Alias("repos")]
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

    [Parameter(ParameterSetName = 'RepositoryFile')]
    [ValidateScript({Test-Path $_ -PathType 'Leaf'})]
    [string]$RepositoryFilePath = "$PSScriptRoot/../data/repositories.txt",

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [hashtable] $Fields,

    [Parameter()]
    [ValidateRange(30, 1000)]
    [int] $Limit = 1000,

    [Parameter()]
    [switch] $Force
)

$OFS = ','

if (!(Get-Command -Type Application gh -ErrorAction Ignore)) {
    throw 'You must first install the GitHub CLI: https://github.com/cli/cli/tree/trunk#installation'
}

if ((gh extension list) -match 'heaths/gh-projects') {
    Write-Verbose 'Making sure GitHub CLI extension heaths/ghprojects is up to date'
    gh extension upgrade heaths/gh-projects | Out-Null
} else {
    Write-Verbose 'Installing GitHub CLI extension heaths/gh-projects'
    gh extension install heaths/gh-projects | Out-Null
}

if ($PSCmdlet.ParameterSetName -eq 'Languages') {
    $Repositories = foreach ($lang in $Languages) {
        "Azure/azure-sdk-for-$lang"
    }
}

if ($PSCmdlet.ParameterSetName -eq 'RepositoryFile') {
    $Repositories = Get-Content $RepositoryFilePath
}

if ($Fields) {
    $fieldArgs = @()
    foreach ($key in $Fields.Keys) {
        $fieldArgs += "$key=`"$($Fields[$key])`""
    }
}

foreach ($repo in $Repositories) {
    Write-Verbose "Getting issues labeled $Labels from $repo"
    $issues = gh issue list --repo $repo --label "$Labels" --limit $Limit --json number | ConvertFrom-Json | Select-Object -ExpandProperty number

    if ($len = $issues.Length) {
        if ($Force -or $PSCmdlet.ShouldProcess(
            "Adding $len issues from $repo to project #${ProjectNumber}",
            "Add $len issues from $repo to project #${ProjectNumber}?",
            "Add issues to project")) {

            # Use array splatting to properly conditionally add --field args.
            $ghArgs = @(
                'projects'
                'edit'
                $ProjectNumber
                '--repo'
                $repo
                '--add-issue'
                "$issues"
            )

            if ($fieldArgs) {
                $ghArgs += '--field', "$fieldArgs"
            }

            gh @ghArgs
        }
    }
}

<#
.SYNOPSIS
Adds issues from Azure SDK language repositories to a project given labels.

.PARAMETER ProjectNumber
The project (beta) number in the Azure organization. This project (beta) should be referenced within the language repository.

.PARAMETER Labels
The required labels used to select issues from each lanuage repository.

.PARAMETER Repositories
The GitHub repositories to query for issues e.g., "Azure/azure-sdk-for-net".

.PARAMETER Languages
The Azure SDK languages to query for issues e.g., "net" for "Azure/azure-sdk-for-net".

.PARAMETER RepositoryFilePath
The fully-qualified path (including filename) to a new line-delmited file of respositories to update.

.PARAMETER Fields
Custom fields defined by the project to set when adding issues.

.PARAMETER Limit
The number of issues to return from each language repository.

.PARAMETER Force
Add issues from each language repository to the project without prompting.

.EXAMPLE
Add-AzsdkProjectIssues.ps1 -ProjectNumber 150 -Labels Client, KeyVault -WhatIf
See how many issues from each language repository would be added to a project without actually adding them.

.EXAMPLE
Add-AzsdkProjectIssues.ps1 -ProjectNumber 150 -Labels Client, KeyVault -Fields @{Status="Todo"}
Add issues labeld "Client" and "KeyVault" to project 150 while setting the "Status" custom field to "Todo".
#>
