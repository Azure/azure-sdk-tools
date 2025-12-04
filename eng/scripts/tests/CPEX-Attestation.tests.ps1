Import-Module Pester

Set-StrictMode -Version 3

# --------------------- Get Triages ---------------------
Describe "Get-TriagesForCPEXAttestation" {
    BeforeAll {
        . (Join-Path $PSScriptRoot "../../common/scripts/Helpers/DevOps-WorkItem-Helpers.ps1")

        Set-Variable -Name capturedFields -Scope Script -Value $null
        Set-Variable -Name capturedWiql   -Scope Script -Value $null
    }

    Context 'builds the correct WIQL and field list' {
        BeforeEach {  
            $script:capturedFields = $null
            $script:capturedWiql   = $null

            Mock -CommandName Invoke-Query -MockWith {
                param($fields, $wiql, $output)

                $script:capturedFields = $fields
                $script:capturedWiql   = $wiql
                
                $workItems = @()
                $workItems += @{
                        id = 1234
                        fields = @{
                            'Custom.ProductServiceTreeID'             = '12345-1232134-1232134'
                            'Custom.ProductType'                      = 'Feature'
                            'Custom.ProductLifecycle'                 = 'Public Preview'
                            'Custom.DataScope'                        = 'yes'
                            'Custom.MgmtScope'                        = 'yes'
                            'Custom.DataplaneAttestationStatus'       = 'Pending'
                            'Custom.ManagementPlaneAttestationStatus' = 'Pending'
                            'Custom.ProductName'                      = 'Contoso Service'
                        }
                    }
                $workItems += @{}
                return $workItems
            } -Verifiable
        }

        It 'calls Invoke-Query exactly once' {
            $result = Get-TriagesForCPEXAttestation

            Assert-MockCalled Invoke-Query -Exactly 1
            $result | should -Not -BeNullOrEmpty
            $result.Count | Should -Be 2
            $result[0].id | Should -Be 1234
        }

        It 'builds the expected field list' {
            $null = Get-TriagesForCPEXAttestation

            $expectedFields = @(
                'Custom.ProductServiceTreeID',
                'Custom.ProductType',
                'Custom.ProductLifecycle',
                'Custom.DataScope',
                'Custom.MgmtScope',
                'Custom.DataplaneAttestationStatus',
                'Custom.ManagementPlaneAttestationStatus',
                'Custom.ProductName'
            )

            foreach($field in $expectedFields) {
                $script:capturedFields | Should -Contain $field
            }
        }

        It 'assembles WIQL with correct work item type, attestation status, tag exclusions, etc' {
            $null = Get-TriagesForCPEXAttestation
            $wiql = $script:capturedWiql
            
            # Normalize whitespace to make matching resilient
            $norm = ($wiql -replace '\s+', ' ').Trim()

            # Work item type is Triage
            $norm | Should -Match "\[System\.WorkItemType\]\s*=\s*'Triage'"

            # Pending Status for either dataplane or management plane
            $norm | Should -Match "\[Custom\.DataplaneAttestationStatus\]\s*IN\s*\(''\s*,\s*'Pending'\)"
            $norm | Should -Match "\[Custom\.ManagementPlaneAttestationStatus\]\s*IN\s*\(''\s*,\s*'Pending'\)"

            # Tag Exclusions
            $norm | Should -Match "\[System\.Tags\]\s*NOT\s*CONTAINS\s*'Release Planner App Test'"
            $norm | Should -Match "NOT\s*CONTAINS\s*'non-APEX tracking'"
            $norm | Should -Match "NOT\s*CONTAINS\s*'APEX out of scope'"

            # Required non-empty fields
            $norm | Should -Match "\[Custom\.ProductServiceTreeID\]\s*<>\s*''"
            $norm | Should -Match "\[Custom\.ProductLifecycle\]\s*<>\s*''"

            # ProductType filter
            $norm | Should -Match "\[Custom\.ProductType\]\s*IN\s*\('Feature'\s*,\s*'Offering'\s*,\s*'Sku'\)"
        }
    }

    Context 'returns empty when no triage work items found' {
        BeforeEach {
            Mock -CommandName Invoke-Query -MockWith {
                param($fields, $wiql, $output)
                @()
            } -Verifiable    
        }

        It 'returns an empty array' {
            $result = Get-TriagesForCPEXAttestation
            Assert-MockCalled Invoke-Query -Exactly 1
            $result | Should -BeNullOrEmpty
        }
    }
}

# --------------------- Get Release Plans ---------------------
Describe "Get-ReleasePlansForCPEXAttestation" {
    BeforeAll {
        . (Join-Path $PSScriptRoot "../../common/scripts/Helpers/DevOps-WorkItem-Helpers.ps1")

        Set-Variable -Name capturedFields -Scope Script -Value $null
        Set-Variable -Name capturedWiql   -Scope Script -Value $null
    }

    Context 'builds the correct WIQL and field list' {
        BeforeEach {  
            $script:capturedFields = $null
            $script:capturedWiql   = $null

            Mock -CommandName Invoke-Query -MockWith {
                param($fields, $wiql, $output)

                $script:capturedFields = $fields
                $script:capturedWiql   = $wiql
                
                $workItems = @()
                $workItems += @{
                        id = 1234
                        fields = @{
                            'Custom.ProductServiceTreeID'             = '12345-1232134-1232134'
                            'Custom.ProductType'                      = 'Feature'
                            'Custom.ReleasePlanType'                  = 'GA'
                            'Custom.DataScope'                        = 'yes'
                            'Custom.MgmtScope'                        = 'no'
                            'Custom.ProductName'                      = 'Contoso Service'
                        }
                    }
                $workItems += @{}
                return $workItems
            } -Verifiable
        }

        It 'calls Invoke-Query exactly once' {
            $result = Get-ReleasePlansForCPEXAttestation

            Assert-MockCalled Invoke-Query -Exactly 1
            $result | should -Not -BeNullOrEmpty
            $result.Count | Should -Be 2
            $result[0].id | Should -Be 1234
        }

        It 'builds the expected field list' {
            $null = Get-ReleasePlansForCPEXAttestation

            $expectedFields = @(
                'Custom.ProductServiceTreeID',
                'Custom.ProductType',
                'Custom.ReleasePlanType',
                'Custom.DataScope',
                'Custom.MgmtScope',
                'Custom.ProductName'
            )

            foreach($field in $expectedFields) {
                $script:capturedFields | Should -Contain $field
            }
        }

        It 'assembles WIQL with correct work item type, completion status, attestation status, tag exclusions, etc' {
            $null = Get-ReleasePlansForCPEXAttestation
            $wiql = $script:capturedWiql
            
            # Normalize whitespace to make matching resilient
            $norm = ($wiql -replace '\s+', ' ').Trim()

            # Work item type is Triage
            $norm | Should -Match "\[System\.WorkItemType\]\s*=\s*'Release Plan'"

            # Release Plan is in Finished State
            $norm | Should -Match "\[System\.State\]\s*=\s*'Finished'"

            # Pending Status
            $norm | Should -Match "\[Custom\.AttestationStatus\]\s*IN\s*\(''\s*,\s*'Pending'\)"

            # Tag Exclusions
            $norm | Should -Match "\[System\.Tags\]\s*NOT\s*CONTAINS\s*'Release Planner App Test'"
            $norm | Should -Match "NOT\s*CONTAINS\s*'non-APEX tracking'"
            $norm | Should -Match "NOT\s*CONTAINS\s*'APEX out of scope'"

            # Required non-empty fields
            $norm | Should -Match "\[Custom\.ProductServiceTreeID\]\s*<>\s*''"
            $norm | Should -Match "\[Custom\.ProductLifecycle\]\s*<>\s*''"

            # ProductType filter
            $norm | Should -Match "\[Custom\.ProductType\]\s*IN\s*\('Feature'\s*,\s*'Offering'\s*,\s*'Sku'\)"
        }
    }

    Context 'returns empty when no release plan work items found' {
        BeforeEach {
            Mock -CommandName Invoke-Query -MockWith {
                param($fields, $wiql, $output)
                @()
            } -Verifiable    
        }

        It 'returns an empty array' {
            $result = Get-ReleasePlansForCPEXAttestation
            Assert-MockCalled Invoke-Query -Exactly 1
            $result | Should -BeNullOrEmpty
        }
    }
}

# --------------------- Update Attestation Status In Work Item ---------------------
Describe "Update-AttestationStatusInWorkItem" {
    BeforeAll {
        . (Join-Path $PSScriptRoot "../../common/scripts/Helpers/DevOps-WorkItem-Helpers.ps1")

        Set-Variable -Name capturedId -Scope Script -Value $null
        Set-Variable -Name capturedFields -Scope Script -Value $null
    }

    BeforeEach {
        $script:FixedDate = [datetime]'2025-12-01T10:00:00Z'
        Mock -CommandName Get-Date -MockWith { $script:FixedDate }
        Mock -CommandName Write-Host -MockWith {}

        Mock -CommandName UpdateWorkItem {
            param(
                [int]    $id,
                [object] $fields,
                [string] $title,
                [string] $state,
                [string] $assignedTo,
                [bool]   $outputCommand
            )

            $script:CapturedId     = $id
            $script:CapturedFields = $fields
        
            @{
                id     = $id
                fields = @{}
            }
        } -Verifiable
    }
    
    It 'Successfully updates attestation status and sets attestation date for all field variants' -TestCases @(
        @{ fieldName = 'Custom.ManagementPlaneAttestationStatus'; expectedDateField = 'Custom.ManagementPlaneAttestationDate' }
        @{ fieldName = 'Custom.DataplaneAttestationStatus'      ; expectedDateField = 'Custom.DataplaneAttestationDate' }
        @{ fieldName = 'Custom.AttestationStatus'               ; expectedDateField = 'Custom.AttestationDate' }
    ) {
        param($fieldName, $expectedDateField)
        $workItemId = 1234
        $status     = 'Completed'

        $result = Update-AttestationStatusInWorkItem -workItemId $workItemId -fieldName $fieldName -status $status

        Assert-MockCalled UpdateWorkItem -Exactly 1
        $result | Should -BeTrue
        
        $id = $script:capturedId
        $fields = $script:capturedFields

        $id | Should -Be $workItemId
        $fields | Where-Object { $_ -like "*$fieldName=$status*" } | Should -Not -BeNullOrEmpty
        $fields | Where-Object { $_ -like "*$expectedDateField=$($script:FixedDate)*" } | Should -Not -BeNullOrEmpty
    }
}