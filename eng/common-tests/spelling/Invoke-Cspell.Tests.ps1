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
        $expectedPackageVersion = '6.31.2'

        # Act
        $actual = &"$PSScriptRoot/../../common/spelling/Invoke-Cspell.ps1" `
            -JobType '--version'

        # Assert
        $actual | Should -Be $expectedPackageVersion
    }
}
