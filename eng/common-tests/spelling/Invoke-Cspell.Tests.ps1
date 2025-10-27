Describe 'Tool Version' {
    BeforeAll {
        $ShouldCleanUpVscodeDirectory = $false

        $cspellConfigPath = "$PSScriptRoot/../../../.vscode/cspell.json"
        if (!(Test-Path $cspellConfigPath)) {
            $ShouldCleanUpVscodeDirectory = $true

            $vscodeDirectory = Split-Path $cspellConfigPath -Parent
            New-Item -ItemType Directory -Path $vscodeDirectory -Force | Out-Null
            $configJsonContent = @"
{
    "version": "0.2",
    "language": "en",
    "ignorePaths": [
        ".vscode/cspell.json",
    ]
}
"@
            $configJsonContent > $cspellConfigPath
        }
    }

    AfterAll {
        if ($ShouldCleanUpVscodeDirectory) {
            Remove-Item -Path $vscodeDirectory -Recurse -Force | Out-Null
        }
    }

    It 'Should have the correct version' -Tag 'UnitTest' {
        # Arrange
        $expectedPackageVersion = '9.2.1'

        # Act
        $actual = &"$PSScriptRoot/../../common/spelling/Invoke-Cspell.ps1" `
            -JobType '--version'

        # Assert
        $actual | Should -Be $expectedPackageVersion
    }

    It 'Should scan all files provided by the -FileList' -Tag 'UnitTest' { 
        $files = @(
            "eng/common-tests/spelling/file1.txt",
            "eng/common-tests/spelling/file2.txt"
        )

        $actual = &"$PSScriptRoot/../../common/spelling/Invoke-Cspell.ps1" `
            -FileList $files 2>&1

        foreach ($file in $files) {
            $actual | Where-Object { $_ -like "*$file*" } | Should -Not -BeNullOrEmpty
        }
    }

    It 'Should scan all files provided via pipeline' -Tag 'UnitTest' { 
        $files = @(
            "eng/common-tests/spelling/file1.txt",
            "eng/common-tests/spelling/file2.txt"
        )

        $actual = $files | &"$PSScriptRoot/../../common/spelling/Invoke-Cspell.ps1" 2>&1

        foreach ($file in $files) {
            $actual | Where-Object { $_ -like "*$file*" } | Should -Not -BeNullOrEmpty
        }
    }
}
