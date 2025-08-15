[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$SourcePath,
  [Parameter(Mandatory = $true)]
  [string]$OutPath
)

$ErrorActionPreference = "Stop"

Write-Host "Generating API review token file: $($SourcePath)"
$installedPath = npm ls @azure-tools/ts-genapi -p
$parserPath = Join-Path -Path $installedPath "export.js"

$FileName = Split-Path -Leaf $SourcePath
$OutFileName = $FileName -replace ".api.json", "_js.json"
$OutFilePath = Join-Path -Path $OutPath $OutFileName
Write-Host "Converting api-extractor file $($SourcePath) to APIview code file $($OutFilePath)"

try {
    node $parserPath $SourcePath $OutFilePath
    if ($LASTEXITCODE -ne 0) {
        throw "Node.js command failed with exit code: $LASTEXITCODE"
    }
} catch {
    Write-Error "Failed to generate APIView token file: $_"
    exit 1
}