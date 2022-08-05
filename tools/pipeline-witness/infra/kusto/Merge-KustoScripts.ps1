param (
  [Parameter(Mandatory=$true)]
  [string]$OutputPath
)

function Merge-Files([string[]]$FileSpecs) {
  $lines = @()

  foreach ($fileSpec in $FileSpecs) {
    $files = Get-Item -Path $fileSpec

    foreach ($file in $files) {
        $Lines += '/////////////////////////////////////////////////////////////////////////////////////////'
        $Lines += "// Imported from $(Resolve-Path $file.FullName -Relative)"
        $Lines += ''
        $Lines += Get-Content $file
        $Lines += ''
        $Lines += ''
    }
  }

  return $lines
}


Merge-Files "$PSScriptRoot/tables/*.kql" | Set-Content (Join-Path $OutputPath 'tables.kql')
Merge-Files "$PSScriptRoot/views/*.kql", "$PSScriptRoot/functions/*.kql" | Set-Content (Join-Path $OutputPath 'functions.kql')
