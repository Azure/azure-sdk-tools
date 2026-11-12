<#
.SYNOPSIS
Create a mail patch from a pull request's final directory changes, including merge-only changes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DirectoryToSync,
    [string]$FilePatternsJson = '[]',
    [Parameter(Mandatory = $true)]
    [string]$TargetBranch,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [string]$CommitMessage = 'Sync directory changes'
)

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$filePatterns = @($FilePatternsJson | ConvertFrom-Json)
if ($filePatterns.Count -eq 0) { $filePatterns = @('*') }
$pathSpecs = @()
foreach ($filePattern in $filePatterns) {
    if ($filePattern -isnot [string]) { throw 'File patterns must be strings.' }
    $pathSpecs += "$DirectoryToSync/$filePattern"
}

$baseCommit = git merge-base HEAD $TargetBranch
if ($LASTEXITCODE -ne 0) { throw "Could not resolve the merge base with '$TargetBranch'." }
$headCommit = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the source commit.' }
git diff --quiet $baseCommit $headCommit -- @pathSpecs
$diffExitCode = $LASTEXITCODE
if ($diffExitCode -eq 0) { throw 'No changes match the requested directory and file patterns.' }
if ($diffExitCode -ne 1) { throw 'Could not inspect the requested directory changes.' }

$patchDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
if ((Test-Path -LiteralPath $patchDirectory) -and @(Get-ChildItem -LiteralPath $patchDirectory -Force).Count -gt 0) {
    throw "Patch output directory is not empty: $patchDirectory"
}
New-Item -ItemType Directory -Path $patchDirectory -Force | Out-Null

$headTree = git rev-parse 'HEAD^{tree}'
if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the source tree.' }
$snapshotCommit = git -c user.name=azure-sdk -c user.email=azuresdk@microsoft.com commit-tree $headTree -p $baseCommit -m $CommitMessage
if ($LASTEXITCODE -ne 0) { throw 'Could not create the temporary sync snapshot.' }
$patchFiles = @(git format-patch -1 $snapshotCommit --binary --full-index --output-directory $patchDirectory -- @pathSpecs)
if ($LASTEXITCODE -ne 0 -or $patchFiles.Count -ne 1) { throw 'Could not create the directory sync mail patch.' }

Write-Information "Created sync patch from $headCommit against merge base $baseCommit." -InformationAction Continue
Write-Information "##vso[task.setvariable variable=PatchFilesLocation]$patchDirectory" -InformationAction Continue
return $patchFiles
