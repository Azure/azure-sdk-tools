Import-Module Pester

BeforeAll {
    . $PSScriptRoot/../common/scripts/Helpers/Package-Helpers.ps1
    $templateDirectory = "$PSScriptRoot/../common/pipelines/templates/steps"
    $script:legacyTemplate = Get-Content "$templateDirectory/validate-all-packages.yml" -Raw | CompatibleConvertFrom-Yaml
    $script:updateTemplate = Get-Content "$templateDirectory/update-package-work-items.yml" -Raw | CompatibleConvertFrom-Yaml
}

Describe "Package work-item update template" -Tag "UnitTest" {
    It "Preserves the legacy parameter contract" {
        $legacyTemplate.parameters.Count | Should -Be 4
        $updateTemplate.parameters.Count | Should -Be $legacyTemplate.parameters.Count
        for ($i = 0; $i -lt $legacyTemplate.parameters.Count; $i++) {
            $updateTemplate.parameters[$i].name | Should -BeExactly $legacyTemplate.parameters[$i].name
            $updateTemplate.parameters[$i].type | Should -BeExactly $legacyTemplate.parameters[$i].type
            ConvertTo-Json -InputObject $updateTemplate.parameters[$i].default |
                Should -BeExactly (ConvertTo-Json -InputObject $legacyTemplate.parameters[$i].default)
        }
    }

    It "Forwards every legacy parameter without adding duplicate steps" {
        $legacyTemplate.steps.Count | Should -Be 1
        $wrapper = $legacyTemplate.steps[0]
        $wrapper.template | Should -BeExactly "update-package-work-items.yml"
        $wrapper.parameters.Count | Should -Be $legacyTemplate.parameters.Count
        foreach ($parameter in $legacyTemplate.parameters) {
            $wrapper.parameters[$parameter.name] | Should -BeExactly ('${{ parameters.' + $parameter.name + ' }}')
        }
    }

    It "Keeps the opt-out and internal non-PR build restrictions" {
        $updateTemplate.steps.Count | Should -Be 1
        $updateTemplate.steps[0].Keys | Should -BeExactly '${{ if and(ne(variables[''Skip.PackageValidation''], ''true''), and(ne(variables[''Build.Reason''], ''PullRequest''), eq(variables[''System.TeamProject''], ''internal''))) }}'
    }

    It "Labels the non-blocking work-item update accurately" {
        $steps = @($updateTemplate.steps[0].Values)[0]
        $steps.Count | Should -Be 2
        $steps[0].displayName | Should -BeExactly "Set as release build"
        $steps[1].displayName | Should -BeExactly "Update package work items"
        $steps[1].task | Should -BeExactly "AzureCLI@2"
        $steps[1].continueOnError | Should -BeTrue
        $steps[1].inputs.inlineScript | Should -Match '/eng/common/scripts/Validate-All-Packages.ps1'
        $steps[1].condition | Should -Match 'succeededOrFailed\(\)'
        $steps[1].condition | Should -Match "not\(endsWith\(variables\['Build.Repository.Name'\], '-pr'\)\)"
    }
}
