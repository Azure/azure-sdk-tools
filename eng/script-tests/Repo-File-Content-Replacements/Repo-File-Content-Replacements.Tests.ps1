<#
```
Invoke-Pester -Output Detailed $PSScriptRoot/Repo-File-Content-Replacements.Tests.ps1
```
#>

Import-Module Pester
Set-StrictMode -Version Latest

BeforeAll {
    function Backup-Folder($targetFolder, $backupFolder) {
        if (!(Test-Path $targetFolder)) {
            return $null
        }
        
        if (!(Test-Path $backupFolder)) {
            New-Item $backupFolder -ItemType Directory
        }
        Copy-Item -Path "$targetFolder/*" -Destination $backupFolder -Recurse -PassThru -Force
    }
    function Reset-Folder($backupFolder, $targetFolder) {
        if (Test-Path $backupFolder) {
            Copy-Item -Path "$backupFolder/*" -Destination $targetFolder -Recurse -Force
            Remove-Item $backupFolder -Recurse
        }
    }
}
AfterAll {
    if (Test-Path "$PSScriptRoot/inputs/backup") {
        Remove-Item "$PSScriptRoot/inputs/backup" -Recurse
    }
}
# Test plan:
# 1. Tests on the path does not match exclude nor include.
# 1. Tests on the path matching exclude paths.
# 2. Tests on the path matching include paths.
# 3. Tests on the path matching both exclude and include.
# 4. Tests on the file match the migration map.
# 5. Tests on the file does not match the migration map.
Describe "repo-file-content-replacement" -Tag "UnitTest" {
    # Passed cases
    It "Test on the files have matching content" -TestCases @(
        @{ 
            exludePaths = "(/|\\)excludes(/|\\)|(/|\\)eng(/|\\)scripts-tests(/|\\)Repo-File-Content-Replacements(/|\\)inputs(/|\\)test1(/|\\)jsonFiles(/|\\)1.json";
            includeFromExcludedPaths = "(/|\\)excludes(/|\\)includes(/|\\)";
            migrationMapFile = "$PSScriptRoot/inputs/test1/jsonFiles/1.json";
            scannedDirectory = "$PSScriptRoot/inputs/test1";
        }
        @{
            exludePaths = "(/|\\)excludes(/|\\)";
            includeFromExcludedPaths = "";
            migrationMapFile = "$PSScriptRoot/inputs/test1/jsonFiles/1.json";
            scannedDirectory = "$PSScriptRoot/inputs/test2";
        }
    ) {
        $backupFolder = "$PSScriptRoot/inputs/backup"
        Backup-Folder -targetFolder $ScannedDirectory -backupFolder $backupFolder
        $migrationMap = Get-Content $migrationMapFile -Raw
        . $PSScriptRoot/../../scripts/Repo-File-Content-Replacements.ps1 `
            -ExcludePathsRegex $exludePaths `
            -IncludeFromExcludedPathsRegex $includeFromExcludedPaths `
            -MigrationMapJson $migrationMap `
            -ScannedDirectory $ScannedDirectory 
        $files = Get-ChildItem "$scannedDirectory/*" -Recurse -File -Include *.txt
        foreach ($file in $files) {
            $expectPath = $file.FullName -replace 'inputs', 'expected'
            (Get-Content $file) | Should -Be (Get-Content $expectPath)
        }
        Reset-Folder -backupFolder $backupFolder -targetFolder $ScannedDirectory
    }
}

Describe "repo-file-content-replacement" -Tag "UnitTest" {
    # Passed cases
    It "Test on the files with no includePaths" -TestCases @(
        @{ 
            exludePaths = "(/|\\)excludes(/|\\)";
            migrationMapFile = "$PSScriptRoot/inputs/test1/jsonFiles/1.json";
            scannedDirectory = "$PSScriptRoot/inputs/test3";
        }
    ) {
        $backupFolder = "$PSScriptRoot/inputs/backup"
        Backup-Folder -targetFolder $ScannedDirectory -backupFolder $backupFolder
        $migrationMap = Get-Content $migrationMapFile -Raw
        . $PSScriptRoot/../../scripts/Repo-File-Content-Replacements.ps1 `
            -ExcludePathsRegex $exludePaths `
            -MigrationMapJson $migrationMap `
            -ScannedDirectory $ScannedDirectory 
        $files = Get-ChildItem "$scannedDirectory/*" -Recurse -File -Include *.txt
        foreach ($file in $files) {
            $expectPath = $file.FullName -replace 'inputs', 'expected'
            (Get-Content $file) | Should -Be (Get-Content $expectPath)
        }
        Reset-Folder -backupFolder $backupFolder -targetFolder $ScannedDirectory
    }
}
