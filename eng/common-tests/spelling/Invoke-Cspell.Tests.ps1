Describe 'Tool Version' {
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
