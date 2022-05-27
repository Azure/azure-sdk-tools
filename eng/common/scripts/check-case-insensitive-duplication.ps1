$files = git ls-files | Group-Object | Where-Object { $_.Count -gt 1 }
if ($files.Count -gt 0) {
    Write-Host "Do NOT have file names only differ in cases. Please double check your file names: "
    $files.Group
}