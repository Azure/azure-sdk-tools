[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$SourcePath,
  [Parameter(Mandatory = $true)]
  [string]$OutPath
)

python -m pip freeze
Write-Host "Generating API review token file: $($SourcePath)"
python -m apistub --pkg-path $SourcePath --out-path $OutPath
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to generate APIView token file. Python apistub command failed with exit code: $LASTEXITCODE"
    exit $LASTEXITCODE
}