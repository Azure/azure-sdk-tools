Import-Module Pester

BeforeAll {
    . $PSScriptRoot/../../common/scripts/Helpers/PSModule-Helpers.ps1
}

Describe "Install-ModuleIfNotInstalled temporary repository cleanup failures" -Tag "UnitTest", "PSModule-Helpers" {
    BeforeEach {
        $script:InstalledModules = @{}
        $script:Repositories = @()
        $script:AvailableModules = @()

        Mock Write-Host { }
        Mock Write-Warning { }
        Mock Get-PSRepository { $script:Repositories }
        Mock Register-PSRepository {
            $script:Repositories += [pscustomobject]@{ Name = $Name; SourceLocation = "$SourceLocation" }
        }
        Mock Unregister-PSRepository { throw 'simulated cleanup failure' }
        Mock Install-Module {
            $script:AvailableModules += [pscustomobject]@{ Name = "$Name"; Version = [version]$RequiredVersion }
        }
        Mock Get-Module { $script:AvailableModules } -ParameterFilter { $ListAvailable }
    }

    It "returns the installed module and warns when cleanup fails" {
        $module = Install-ModuleIfNotInstalled -moduleName 'TestModule' -version '1.2.3'

        $module.Name | Should -Be 'TestModule'
        $module.Version | Should -Be ([version]'1.2.3')
        Should -Invoke Unregister-PSRepository -Times 1 -Exactly
        Should -Invoke Write-Warning -Times 1 -Exactly -ParameterFilter { $Message -like '*simulated cleanup failure*' }
    }

    It "throws the install error and warns when cleanup also fails" {
        Mock Install-Module { throw 'simulated install failure' }

        { Install-ModuleIfNotInstalled -moduleName 'TestModule' -version '1.2.3' } | Should -Throw -ExpectedMessage 'simulated install failure'
        Should -Invoke Unregister-PSRepository -Times 1 -Exactly
        Should -Invoke Write-Warning -Times 1 -Exactly -ParameterFilter { $Message -like '*simulated cleanup failure*' }
    }
}
