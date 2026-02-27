Describe 'Invoke-Cspell' -Tag 'UnitTest' {
    BeforeAll {
        $workingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
        $packageInstallCache = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())

        Write-Host "Create test temp dir: $workingDirectory"
        New-Item -ItemType Directory -Force -Path $workingDirectory | Out-Null
        New-Item -ItemType Directory -Force -Path "$workingDirectory/.vscode" | Out-Null

        $configJsonContent = @"
{
    "version": "0.2",
    "language": "en"
}
"@
        $configJsonContent > "$workingDirectory/.vscode/cspell.json"
        $script:cspellConfigPath = "$workingDirectory/.vscode/cspell.json"
        $script:workingDirectory = $workingDirectory
        $script:packageInstallCache = $packageInstallCache
    }

    AfterAll {
        Write-Host "Remove test temp dir: $workingDirectory"
        Remove-Item -Path $workingDirectory -Recurse -Force | Out-Null
        if (Test-Path $packageInstallCache) {
            Remove-Item -Path $packageInstallCache -Recurse -Force | Out-Null
        }
    }

    It 'Detects spelling errors without AdditionalParams' {
        # Arrange - create a file with known misspellings
        $testFile = "$script:workingDirectory/misspelled.txt"
        "thiss iz a badd spelllling eror" > $testFile

        # Act
        &"$PSScriptRoot/../../common/spelling/Invoke-Cspell.ps1" `
            -FileList @($testFile) `
            -CSpellConfigPath $script:cspellConfigPath `
            -SpellCheckRoot $script:workingDirectory `
            -PackageInstallCache $script:packageInstallCache `
            -LeavePackageInstallCache

        # Assert - cspell exits non-zero when spelling errors are found
        $LASTEXITCODE | Should -Not -Be 0
    }

    It 'Passes AdditionalParams to cspell (--no-exit-code suppresses non-zero exit)' {
        # Arrange - create a file with known misspellings
        $testFile = "$script:workingDirectory/misspelled-no-exit.txt"
        "thiss iz a badd spelllling eror" > $testFile

        # Act - pass --no-exit-code so cspell returns 0 even when errors are found
        &"$PSScriptRoot/../../common/spelling/Invoke-Cspell.ps1" `
            -FileList @($testFile) `
            -CSpellConfigPath $script:cspellConfigPath `
            -SpellCheckRoot $script:workingDirectory `
            -PackageInstallCache $script:packageInstallCache `
            -LeavePackageInstallCache `
            -AdditionalParams @('--no-exit-code')

        # Assert - AdditionalParams were forwarded to cspell, suppressing non-zero exit code
        $LASTEXITCODE | Should -Be 0
    }
}
