Import-Module Pester

Set-StrictMode -Version 3

# --------------------- Get Triages ---------------------
Describe "Get-TriagesForCPEXAttestation" {
    BeforeAll {
        . (Join-Path $PSScriptRoot "../../common/scripts/Helpers/DevOps-WorkItem-Helpers.ps1")

        Set-Variable -Name CapturedFields -Scope Script -Value $null
        Set-Variable -Name CapturedWiql   -Scope Script -Value $null

        Mock -CommandName Invoke-RestMethod -MockWith {}
    }

    Context 'builds the correct WIQL and field list' {
        BeforeEach {  
            $script:CapturedFields = $null
            $script:CapturedWiql   = $null

            Mock -CommandName Invoke-Query -MockWith {
                param($fields, $wiql, $output)

                $script:CapturedFields = $fields
                $script:CapturedWiql   = $wiql
                
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
                $script:CapturedFields | Should -Contain $field
            }
        }

        It 'assembles WIQL with correct work item type, attestation status, tag exclusions, etc' {
            $null = Get-TriagesForCPEXAttestation
            $wiql = $script:CapturedWiql
            
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

        Set-Variable -Name CapturedFields -Scope Script -Value $null
        Set-Variable -Name CapturedWiql   -Scope Script -Value $null

        Mock -CommandName Invoke-RestMethod -MockWith {}
    }

    Context 'builds the correct WIQL and field list' {
        BeforeEach {  
            $script:CapturedFields = $null
            $script:CapturedWiql   = $null

            Mock -CommandName Invoke-Query -MockWith {
                param($fields, $wiql, $output)

                $script:CapturedFields = $fields
                $script:CapturedWiql   = $wiql
                
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
                $script:CapturedFields | Should -Contain $field
            }
        }

        It 'assembles WIQL with correct work item type, completion status, attestation status, tag exclusions, etc' {
            $null = Get-ReleasePlansForCPEXAttestation
            $wiql = $script:CapturedWiql
            
            # Normalize whitespace to make matching resilient
            $norm = ($wiql -replace '\s+', ' ').Trim()

            # Work item type is Release Plan
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

        Set-Variable -Name CapturedId -Scope Script -Value $null
        Set-Variable -Name CapturedFields -Scope Script -Value $null

        Mock -CommandName Invoke-RestMethod -MockWith {}
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
        
        $id = $script:CapturedId
        $fields = $script:CapturedFields

        $id | Should -Be $workItemId
        $fields | Where-Object { $_ -like "*$fieldName=$status*" } | Should -Not -BeNullOrEmpty
        $fields | Where-Object { $_ -like "*$expectedDateField=$($script:FixedDate)*" } | Should -Not -BeNullOrEmpty
    }
}

# --------------------- Parse Release Plans  ---------------------
Describe 'Parse release plans' {
    $releasePlans = @(
        @{ 
            releasePlan = @{ 
                fields = @{
                    "Custom.DataScope" = "Yes"
                    "Custom.MgmtScope" = "No"
                    "Custom.ReleasePlanType" = "APEX Private Preview"
                    "Custom.ProductServiceTreeID" = "123456789-09876541-23124981234"
                    "Custom.ProductType" = "Feature"
                    "Custom.ProductName" = "Product Name"
                }; 
                url = "Fake URL";
                id = "0"
            }; 
            expectation = @{
                productID = "123456789-09876541-23124981234"
                kpiId = "dfe9c112-416e-4e0a-8012-4a3a29807782"
                status = 1
                productType = "Feature"
                url = "Fake Url"
                productName = "Product Name"
            }
        },
        @{ 
            releasePlan = @{ 
                fields = @{
                    "Custom.DataScope" = "No"
                    "Custom.MgmtScope" = "Yes"
                    "Custom.ReleasePlanType" = "Private Preview"
                    "Custom.ProductServiceTreeID" = "123456789-09876541-23124981234"
                    "Custom.ProductType" = "Sku"
                    "Custom.ProductName" = "Product Name"
                }; 
                url = "Fake Url";
                id = "0"
            }; 
            expectation = @{
                productID = "123456789-09876541-23124981234"
                kpiId = "e0504da9-8897-41db-a75f-5027298ba410"
                status = 1
                productType = "ProductSku"
                url = "Fake Url"
                productName = "Product Name"
            }
        },
        @{ 
            releasePlan = @{ 
                fields = @{
                    "Custom.DataScope" = "Yes"
                    "Custom.MgmtScope" = "No"
                    "Custom.ReleasePlanType" = "APEX Public Preview"
                    "Custom.ProductServiceTreeID" = "123456789-09876541-23124981234"
                    "Custom.ProductType" = "Sku"
                    "Custom.ProductName" = "Product Name"
                }; 
                url = "Fake Url";
                id = "0"
            }; 
            expectation = @{
                productID = "123456789-09876541-23124981234"
                kpiId = "ad70777b-a1f5-4d77-8926-5c466d7a214d"
                status = 1
                productType = "ProductSku"
                url = "Fake Url"
                productName = "Product Name"
            }
        },
        @{ 
            releasePlan = @{ 
                fields = @{
                    "Custom.DataScope" = "No"
                    "Custom.MgmtScope" = "Yes"
                    "Custom.ReleasePlanType" = " Public Preview"
                    "Custom.ProductServiceTreeID" = "123456789-09876541-23124981234"
                    "Custom.ProductType" = "Offering"
                    "Custom.ProductName" = "Product Name"
                }; 
                url = "Fake Url";
                id = "0"
            }; 
            expectation = @{
                productID = "123456789-09876541-23124981234"
                kpiId = "84715402-4f3c-4dca-b330-f05206abaec5"
                status = 1
                productType = "Offering"
                url = "Fake Url"
                productName = "Product Name"
            }
        },
        @{ 
            releasePlan = @{ 
                fields = @{
                    "Custom.DataScope" = "Yes"
                    "Custom.MgmtScope" = "No"
                    "Custom.ReleasePlanType" = "   GA  "
                    "Custom.ProductServiceTreeID" = "123456789-09876541-23124981234"
                    "Custom.ProductType" = "Sku"
                    "Custom.ProductName" = "Product Name"
                }; 
                url = "Fake Url";
                id = "0"
            }; 
            expectation = @{
                productID = "123456789-09876541-23124981234"
                kpiId = "da768dff-8f90-4999-ad3a-adcd790911f3"
                status = 1
                productType = "ProductSku"
                url = "Fake Url"
                productName = "Product Name"
            }
        },
        @{ 
            releasePlan = @{ 
                fields = @{
                    "Custom.DataScope" = "No"
                    "Custom.MgmtScope" = "Yes"
                    "Custom.ReleasePlanType" = "GA"
                    "Custom.ProductServiceTreeID" = "123456789-09876541-23124981234"
                    "Custom.ProductType" = "Feature"
                    "Custom.ProductName" = "Product Name"
                }; 
                url = "Fake Url";
                id = "0"
            }; 
            expectation = @{
                productID = "123456789-09876541-23124981234"
                kpiId = "210c095f-b3a2-4cf4-a899-eaab4c3ed958"
                status = 1
                productType = "Feature"
                url = "Fake Url"
                productName = "Product Name"
            }
        }
    )

    BeforeAll {
        . (Join-Path $PSScriptRoot "../../common/scripts/Helpers/DevOps-WorkItem-Helpers.ps1")

        Mock -CommandName Write-Host -MockWith {}
        Mock -CommandName Write-Error -MockWith {}
        Mock -CommandName Write-Warning -MockWith {}
        Mock -CommandName Invoke-RestMethod -MockWith {}
        Mock -CommandName Get-TriagesForCPEXAttestation -MockWith { @() }
        Mock -CommandName Update-AttestationStatusInWorkItem -MockWith {} -Verifiable

        function AddAttestationEntry {
            "placeholder function"
        }

        function SendEmailNotification {
            "placeholder function"
        }
    }

    It 'Successfully parses a release plan; adds to database; updates work item' -TestCases $releasePlans {
        param($releasePlan, $expectation)

        Mock -CommandName Get-ReleasePlansForCPEXAttestation -MockWith { $releasePlan } -Verifiable
        Mock -CommandName AddAttestationEntry -MockWith {} -Verifiable
        Mock -CommandName SendEmailNotification -MockWith {}
        
        & (Join-Path $PSScriptRoot '../Invoke-CPEX-Attestation-Automation.ps1') -AzureSDKEmailUri "FAKE-URI" -TableName "FAKE-TABLE-NAME"

        Should -Invoke -CommandName Get-ReleasePlansForCPEXAttestation -Times 1

        Should -Invoke -CommandName AddAttestationEntry -Times 1
        Should -Invoke -CommandName AddAttestationEntry -Times 1 -ParameterFilter {
            $targetId -eq $expectation.productID
            $actionItemId -eq $expectation.kpiId
            $status -eq $expectation.status
            $targetType -eq $expectation.productType -and
            $url -eq $expectation.url -and 
            $productName -eq $expectation.productName
        }

        Should -Invoke -CommandName Update-AttestationStatusInWorkItem -Times 1
        Should -Invoke -CommandName Update-AttestationStatusInWorkItem -Times 1 -ParameterFilter {
            $workItemId -eq $releasePlan.id -and
            $fieldName -eq "Custom.AttestationStatus" -and
            $status -eq "Completed"
        }
    }
}

# --------------------- Add Attestation Entry to Kusto Database ---------------------
Describe 'Add Attestation Entry to Kusto Database' {
    It 'posts a valid JSON envelope and CSL when run with required params' {
        Mock -CommandName Invoke-RestMethod -MockWith {} -Verifiable
        Mock -CommandName Write-Host -MockWith {}

        $tableName    = 'FAKE-TABLE-NAME'
        $actionItemId = '84715402-4f3c-4dca-b330-f05206abaec5'
        $targetId     = '11314123-2343-1232-2133-213412341344'
        $targetType   = 'ProductSku'
        $status       = 1
        $evidenceUrl  = 'https://dev.azure.com/azure-sdk/Release/_workitems/edit/42'
        
        & (Join-Path $PSScriptRoot '../Add-CPEX-Attestation-Entry.ps1') -TableName $tableName -ActionItemId $actionItemId -TargetId $targetId -TargetType $targetType -Status $status -EvidenceUrl $evidenceUrl

        Should -Invoke -CommandName Invoke-RestMethod -Times 1 -ParameterFilter {
            $Method -eq 'Post' -and
            $ContentType -eq 'application/json' -and
            $Uri -match '/v1/rest/mgmt$' -and
            $Headers.Authorization -like 'Bearer *' -and
            ($Body | ConvertFrom-Json).db -eq 'CPEX_Attestation_DB' -and
            ($Body | ConvertFrom-Json).csl -match ([regex]::Escape($tableName)) -and
            ($Body | ConvertFrom-Json).csl -match ('ActionItemId\s*=\s*"' + [regex]::Escape($actionItemId) + '"') -and
            ($Body | ConvertFrom-Json).csl -match ('TargetId\s*=\s*"' + [regex]::Escape($targetId) + '"') -and
            ($Body | ConvertFrom-Json).csl -match ('TargetType\s*=\s*"' + [regex]::Escape($targetType) + '"') -and
            ($Body | ConvertFrom-Json).csl -match ('Status\s*=\s*int\s*\(\s*' + [regex]::Escape($status.ToString()) + '\s*\)') -and
            ($Body | ConvertFrom-Json).csl -match ('EvidenceUrl\s*=\s*"' + [regex]::Escape($evidenceUrl) + '"') 
            ($Body | ConvertFrom-Json).csl -match 'CreatedTime\s*=\s*datetime\s*\(\s*now\s*\)'
        }
    }
}