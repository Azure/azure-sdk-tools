<#
.SYNOPSIS
Sparse-checks out merged SDK descriptors for the trusted post-merge preparation pipeline.
.DESCRIPTION
Owns the Git trust boundary: resolves the SDK default branch, verifies commit ancestry and
tracked current/previous paths, disables hooks, and checks out the exact requested SHA.
Does not create definitions, grant permissions, or submit releases.
#>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Directory,
    [string] $RepositoryName = $env:PREPARATION_REPOSITORY,
    [string] $MergeCommitSha = $env:PREPARATION_SHA,
    [string] $DescriptorPaths = $env:PREPARATION_DESCRIPTORS
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

function Invoke-CheckoutGit {
    param([string[]] $Arguments, [string] $WorkingDirectory = '')
    $gitArguments = @('-c', 'core.hooksPath=/dev/null')
    if ($WorkingDirectory) { $gitArguments += @('-C', $WorkingDirectory) }
    $output = @(& git @gitArguments @Arguments)
    if ($LASTEXITCODE -ne 0) {
        throw "SDK checkout Git command failed ($LASTEXITCODE): $($Arguments -join ' ')"
    }
    return $output
}

if ($env:BUILD_REPOSITORY_NAME -cne 'Azure/azure-sdk-tools' -or
    $env:BUILD_SOURCEBRANCH -cne 'refs/heads/main' -or
    $env:SYSTEM_TEAMPROJECT -cne 'internal' -or
    $env:SYSTEM_COLLECTIONURI -cne 'https://dev.azure.com/azure-sdk/') {
    throw 'SDK checkout must run from the trusted tools default-branch pipeline in azure-sdk/internal.'
}
if ($RepositoryName -cnotin @('azure-sdk-for-java', 'azure-sdk-for-js', 'azure-sdk-for-python', 'azure-sdk-for-net', 'azure-sdk-for-go')) {
    throw "Unsupported SDK repository '$RepositoryName'."
}
if ($MergeCommitSha -cnotmatch '\A[0-9a-f]{40}\z') {
    throw 'MergeCommitSha must be a full lowercase Git SHA.'
}
$descriptors = ConvertFrom-Json -InputObject $DescriptorPaths -AsHashtable -NoEnumerate
if ($descriptors -isnot [array] -or $descriptors.Count -lt 1 -or $descriptors.Count -gt 100) {
    throw 'DescriptorPaths must contain between 1 and 100 descriptors.'
}
foreach ($descriptor in $descriptors) {
    if ($descriptor -isnot [System.Collections.IDictionary] -or -not $descriptor.Contains('path') -or
        @($descriptor.Keys | Where-Object { $_ -cnotin @('path', 'previousPath') }).Count -gt 0) {
        throw 'Each descriptor requires path and optional previousPath only.'
    }
    foreach ($value in $descriptor.Values) {
        if ($value -isnot [string] -or $value -cnotmatch '\Asdk/(?:[\p{L}\p{N}_.-]+/)+ci(?:\.\w+)?\.yml\z' -or
            @($value.Split('/') | Where-Object { $_ -in @('.', '..') }).Count -gt 0) {
            throw "Unsupported descriptor path '$value'."
        }
    }
}
$Directory = [System.IO.Path]::GetFullPath($Directory)
if (Test-Path -LiteralPath $Directory) {
    throw "SDK checkout directory already exists: $Directory"
}
$remote = "https://github.com/Azure/$RepositoryName.git"
# Resolve HEAD with Git rather than the unauthenticated GitHub REST API, which is rate limited on shared CI agents.
$branch = $null
foreach ($line in @(Invoke-CheckoutGit @('ls-remote', '--symref', $remote, 'HEAD'))) {
    if ($line -cmatch '\Aref: refs/heads/(?<branch>[^\t]+)\tHEAD\z') {
        $branch = $Matches['branch']
        break
    }
}
if (-not $branch) {
    throw "Unable to resolve the default branch for Azure/$RepositoryName."
}
if ($branch -cne 'main') {
    throw "Default branch '$branch' needs a release-gate compatibility review before enabling preparation."
}
Write-Host "Sparse-checking out Azure/$RepositoryName at $MergeCommitSha from $branch history into $Directory"
# tree:0 matches the shared sparse-checkout pattern; Git lazily fetches only the trees and blobs it reads.
$null = Invoke-CheckoutGit @('clone', '--filter=tree:0', '--no-checkout', '--single-branch', '--branch', $branch,
    $remote, $Directory)
$null = Invoke-CheckoutGit @('cat-file', '-e', "${MergeCommitSha}^{commit}") $Directory
$null = Invoke-CheckoutGit @('merge-base', '--is-ancestor', $MergeCommitSha, "refs/remotes/origin/$branch") $Directory
foreach ($descriptor in $descriptors) {
    $entry = @(Invoke-CheckoutGit @('ls-tree', $MergeCommitSha, '--', $descriptor.path) $Directory)
    if ($entry.Count -ne 1 -or $entry[0] -cnotmatch '\A100(?:644|755) blob [0-9a-f]+\t[^\r\n]+\z') {
        throw "Descriptor '$($descriptor.path)' is missing or is not a regular tracked file at the merged SHA."
    }
    if ($descriptor.Contains('previousPath') -and
        @(Invoke-CheckoutGit @('ls-tree', $MergeCommitSha, '--', $descriptor.previousPath) $Directory).Count -ne 0) {
        throw "Previous descriptor '$($descriptor.previousPath)' still exists; this is not a supported relocation."
    }
}
$null = Invoke-CheckoutGit @('sparse-checkout', 'set', '--no-cone', '/sdk/**/*.yml') $Directory
$null = Invoke-CheckoutGit @('checkout', '--detach', $MergeCommitSha, '--') $Directory
$head = @(Invoke-CheckoutGit @('rev-parse', 'HEAD') $Directory)
if ($head.Count -ne 1 -or $head[0] -cne $MergeCommitSha) {
    throw 'SDK checkout HEAD does not match MergeCommitSha.'
}
