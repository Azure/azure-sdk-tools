# One-off removal of the verified synthetic storage-smoke archive, never real evaluation history.
#requires -Version 7.4
[CmdletBinding(SupportsShouldProcess)]
param()

Set-StrictMode -Version 4
$ErrorActionPreference = 'Stop'

if ($env:SYSTEM_COLLECTIONURI -cne 'https://dev.azure.com/azure-sdk/' -or
    $env:SYSTEM_TEAMPROJECT -cne 'internal' -or $env:SYSTEM_DEFINITIONID -cne '8424' -or
    $env:BUILD_REPOSITORY_NAME -cne 'Azure/azure-sdk-tools' -or
    $env:BUILD_SOURCEBRANCH -cne 'refs/heads/test/eval-cli-linux-probe' -or $env:BUILD_REASON -cne 'Manual') {
    throw 'Not the explicitly authorized smoke-cleanup pipeline.'
}

function Invoke-StorageJson([string[]] $Arguments) {
    $output = az @Arguments --only-show-errors --output json 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'Smoke archive storage operation failed; no raw Azure response was logged.' }
    $stdout = @($output | Where-Object { $_ -isnot [Management.Automation.ErrorRecord] }) -join "`n"
    return ConvertFrom-Json -InputObject $stdout -AsHashtable
}

$name = 'v1/azure-sdk/internal/8255/6865368/1/dashboard-bundle.zip'
$expectedHash = 'a55bf3765dd47fa98f03cd1972145765c608497222706272174e55dc7a73f78b'
$storage = @('--account-name', 'evaltestsummary', '--container-name', 'vally-results', '--name', $name, '--auth-mode', 'login')
$present = Invoke-StorageJson (@('storage', 'blob', 'exists') + $storage)
if ($present.exists -eq $false) {
    Write-Host 'Synthetic smoke archive 6865368 is already absent; no deletion was attempted.'
    return
}
if ($present.exists -ne $true) { throw 'Smoke archive existence was not confirmed.' }

$saved = Invoke-StorageJson (@('storage', 'blob', 'show') + $storage)
if ($saved.name -cne $name -or $saved.metadata.sha256 -cne $expectedHash -or
    $saved.metadata.schema -cne '1' -or $saved.properties.contentLength -ne 1647 -or !$saved.properties.etag) {
    throw 'Stored object differs from the pinned synthetic smoke archive; nothing was deleted.'
}
$etag = [string] $saved.properties.etag
$file = Join-Path $env:AGENT_TEMPDIRECTORY ('smoke-6865368-' + [Guid]::NewGuid().ToString('N') + '.zip')
$null = Invoke-StorageJson (@('storage', 'blob', 'download') + $storage + @('--file', $file, '--if-match', $etag, '--no-progress'))
if ((Get-Item -LiteralPath $file).Length -ne 1647 -or
    (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expectedHash) {
    throw 'Smoke archive bytes do not match the original checksum; nothing was deleted.'
}
$zip = [IO.Compression.ZipFile]::OpenRead($file)
try {
    $entry = $zip.GetEntry('manifest.json')
    if (!$entry -or $entry.Length -gt 65536) { throw 'Invalid smoke manifest; nothing was deleted.' }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable }
    finally { $reader.Dispose() }
} finally { $zip.Dispose() }
if ($manifest.schemaVersion -ne 1 -or $manifest.adoOrganization -cne 'azure-sdk' -or
    $manifest.adoProject -cne 'internal' -or $manifest.repo -cne 'Azure/azure-sdk-tools' -or
    [string] $manifest.pipelineDefinitionId -cne '8255' -or [string] $manifest.buildId -cne '6865368' -or
    $manifest.summaryAttempt -ne 1 -or $manifest.pipeline -cne 'Azure-sdk-tools-workflow-eval [synthetic storage smoke]') {
    throw 'Manifest is not the requested synthetic smoke test; nothing was deleted.'
}
Write-Host 'Verified synthetic storage-smoke build 6865368: original manifest, size, checksum and ETag.'
if (!$PSCmdlet.ShouldProcess($name, 'Delete this single verified synthetic smoke archive')) { return }
$null = Invoke-StorageJson (@('storage', 'blob', 'delete') + $storage + @('--if-match', $etag))
$remaining = Invoke-StorageJson (@('storage', 'blob', 'exists') + $storage)
if ($remaining.exists -ne $false) { throw 'Smoke archive deletion could not be confirmed; do not retry without checking storage.' }
Write-Host 'DELETION VERIFIED: only synthetic storage-smoke archive 6865368 was removed. Storage account, container and real results are unchanged.'