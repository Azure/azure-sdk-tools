[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$SourcePath,
  [Parameter(Mandatory = $true)]
  [string]$OutPath,
  [Parameter(Mandatory = $true)]
  [string]$ParserPath
)

Write-Host "Generating API review token file: $($SourcePath)"
$FileName = Split-Path -Leaf $SourcePath
$OutFileName = $FileName -replace ".swagger", "_swagger.json"
$OutFilePath = Join-Path -Path $OutPath $OutFileName
Write-Host "Converting Swagger file $($SourcePath) to APIview code file $($OutFilePath)"
&($ParserPath) --swaggers $SourcePath --output $OutFilePath
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to generate APIView token file. Swagger parser command failed with exit code: $LASTEXITCODE"
    exit $LASTEXITCODE
}