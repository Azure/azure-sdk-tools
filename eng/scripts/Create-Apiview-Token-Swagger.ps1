[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$SourcePath,
  [Parameter(Mandatory = $true)]
  [string]$OutPath,
  [string]$ParserPath = ""
)

Write-Host "Generating API review token file: $($SourcePath)"
$FileName = Split-Path -Leaf $SourcePath
$OutFileName = $FileName -replace ".swagger", "_swagger.json"
$OutFilePath = Join-Path -Path $OutPath $OutFileName
Write-Host "Converting Swagger file $($SourcePath) to APIview code file $($OutFilePath)"
Write-Host "Workspace: $(Pipeline.Workspace)"
Write-Host "Swagger path: $(SwaggerParserInstallPath)"
Write-Host "Swagger path2: $($env:SwaggerParserInstallPath)"
if ($ParserPath -eq "")
{
  $ParserPath = Join-Path -Path $($env:SwaggerParserInstallPath) "SwaggerApiParser/SwaggerApiParser"
}
Write-Host "Parser Path: $($ParserPath)"
Get-Item $ParserPath
&($ParserPath) $SourcePath --output $OutFilePath