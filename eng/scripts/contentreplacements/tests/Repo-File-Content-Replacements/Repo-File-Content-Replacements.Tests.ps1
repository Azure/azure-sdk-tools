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
            Remove-Item $backupFolder -Recurse -Force
        }
    }
}
AfterAll {
    if (Test-Path "$PSScriptRoot/inputs/backup") {
        Remove-Item "$PSScriptRoot/inputs/backup" -Recurse -Force
    }
}
# Test cases:
# 1. Tests on specify valid both include and exclude paths
# 1. Tests on specify exclude paths and pass empty to include paths.
# 2. Tests on specify exclude paths only and no include paths.

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
        . $PSScriptRoot/../../Repo-File-Content-Replacements.ps1 `
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
        . $PSScriptRoot/../../Repo-File-Content-Replacements.ps1 `
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
