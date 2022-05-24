[CmdletBinding()]
Param (
    [Parameter()]
    [string] $ServiceDirectory
)
$scannedDirectory = @{}
if ($ServiceDirectory){
    $scannedDirectory[$ServiceDirectory] = $true
}
else {
    $changedFiles = & "eng/common/scripts/get-changedfiles.ps1"
    $changedFiles | ForEach-Object { $scannedDirectory[(Split-Path -Path $_)] = $true}
}

$fileNameSet = @{}
foreach ($directory in $scannedDirectory.Keys) {
    $files = Get-ChildItem -Path $directory -File -Recurse
    foreach ($file in $files) {
        if ($file.FullName.ToLower() -in $fileNameSet.Keys) {
            Write-Error "There are duplicate files only differ in cases. Please duplicate files as below:"
            Write-Error "   $($file.FullName). "
            Write-Error "   $($fileNameSet[$($file.FullName)])"
            exit 1
        }
        $fileNameSet[$file.FullName.ToLower()] = $file.FullName
    }
}