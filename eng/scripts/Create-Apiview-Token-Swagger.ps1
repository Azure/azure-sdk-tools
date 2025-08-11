[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$SourcePath,
  [Parameter(Mandatory = $true)]
  [string]$OutPath,
  [Parameter(Mandatory = $true)]
  [string]$ParserPath
)

$ErrorActionPreference = "Stop"

Write-Host "Generating API review token file: $($SourcePath)"
$FileName = Split-Path -Leaf $SourcePath
$OutFileName = $FileName -replace ".swagger", "_swagger.json"
$OutFilePath = Join-Path -Path $OutPath $OutFileName
Write-Host "Converting Swagger file $($SourcePath) to APIview code file $($OutFilePath)"

try {
    &($ParserPath) --swaggers $SourcePath --output $OutFilePath
    if ($LASTEXITCODE -ne 0) {
        throw "Swagger parser command failed with exit code: $LASTEXITCODE"
    }
} catch {
    Write-Error "Failed to generate APIView token file: $_"
    exit 1
}