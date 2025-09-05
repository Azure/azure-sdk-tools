[CmdletBinding()]
param (
  [Parameter(Mandatory = $true)]
  [string]$SourcePath,
  [Parameter(Mandatory = $true)]
  [string]$OutPath
)

$ErrorActionPreference = "Stop"

python -m pip freeze
Write-Host "Generating API review token file: $($SourcePath)"

try {
    python -m apistub --pkg-path $SourcePath --out-path $OutPath
    if ($LASTEXITCODE -ne 0) {
        throw "Python apistub command failed with exit code: $LASTEXITCODE"
    }
} catch {
    Write-Error "Failed to generate APIView token file: $_"
    exit 1
}