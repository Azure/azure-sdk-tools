Import-Module Pester

BeforeAll {
    . $PSScriptRoot/../common/scripts/ChangeLog-Operations.ps1
}

Describe "New-ChangelogContent" {
    It "Should parse changelog text with multiple sections" {
        $changelogText = "### Breaking Changes`n`n- Removed deprecated API ``oldMethod()```n- Changed return type of ``getData()```n`n### Features Added`n`n- Added new ``newMethod()`` API`n- Added support for async operations`n`n### Bugs Fixed`n`n- Fixed null pointer exception in ``processData()``"

        $result = New-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "#"

        $result | Should -Not -BeNullOrEmpty
        $result.ReleaseContent | Should -Not -BeNullOrEmpty
        $result.Sections | Should -Not -BeNullOrEmpty
        $result.Sections.Keys | Should -HaveCount 3
        $result.Sections.ContainsKey("Breaking Changes") | Should -BeTrue
        $result.Sections.ContainsKey("Features Added") | Should -BeTrue
        $result.Sections.ContainsKey("Bugs Fixed") | Should -BeTrue
    }

    It "Should handle single section changelog" {
        $changelogText = "### Features Added`n`n- Added new feature X`n- Added new feature Y"

        $result = New-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "#"

        $result | Should -Not -BeNullOrEmpty
        $result.Sections.Keys | Should -HaveCount 1
        $result.Sections.ContainsKey("Features Added") | Should -BeTrue
        $result.Sections["Features Added"] | Should -Contain ""
        $result.Sections["Features Added"] | Should -Contain "- Added new feature X"
        $result.Sections["Features Added"] | Should -Contain "- Added new feature Y"
    }

    It "Should handle empty changelog text sections with content before first section" {
        $changelogText = "Some introductory text`n`n### Breaking Changes`n`n- Change 1"

        $result = New-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "#"

        $result | Should -Not -BeNullOrEmpty
        $result.Sections.Keys | Should -HaveCount 1
        # The intro text should be in ReleaseContent but not in any section
        $result.ReleaseContent | Should -Contain "Some introductory text"
    }

    It "Should respect InitialAtxHeader parameter" {
        # With ## as initial header, section headers are ####
        $changelogText = "#### Breaking Changes`n`n- Some breaking change"

        $result = New-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "##"

        $result | Should -Not -BeNullOrEmpty
        $result.Sections.Keys | Should -HaveCount 1
        $result.Sections.ContainsKey("Breaking Changes") | Should -BeTrue
    }

    It "Should return empty sections when no section headers found" {
        $changelogText = "Just some text without any section headers`nAnd another line"

        $result = New-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "#"

        $result | Should -Not -BeNullOrEmpty
        $result.Sections.Keys | Should -HaveCount 0
        $result.ReleaseContent | Should -Not -BeNullOrEmpty
    }

    It "Should handle Windows-style line endings" {
        $changelogText = "### Features Added`r`n`r`n- Feature 1`r`n- Feature 2"

        $result = New-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "#"

        $result | Should -Not -BeNullOrEmpty
        $result.Sections.ContainsKey("Features Added") | Should -BeTrue
    }

    It "Should handle Unix-style line endings" {
        $changelogText = "### Features Added`n`n- Feature 1`n- Feature 2"

        $result = New-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "#"

        $result | Should -Not -BeNullOrEmpty
        $result.Sections.ContainsKey("Features Added") | Should -BeTrue
    }
}

Describe "Set-ChangeLogEntryContent" {
    It "Should update changelog entry with new content" {
        # Create a mock changelog entry
        $entry = [PSCustomObject]@{
            ReleaseVersion = "1.0.0"
            ReleaseStatus = "(Unreleased)"
            ReleaseTitle = "## 1.0.0 (Unreleased)"
            ReleaseContent = @()
            Sections = @{}
        }

        $newContent = "### Features Added`n`n- Added new feature A`n- Added new feature B"

        $result = Set-ChangeLogEntryContent -ChangeLogEntry $entry -NewContent $newContent -InitialAtxHeader "#"

        $result | Should -Not -BeNullOrEmpty
        $result.ReleaseContent | Should -Not -BeNullOrEmpty
        $result.Sections.ContainsKey("Features Added") | Should -BeTrue
        $result.Sections["Features Added"] | Should -Contain "- Added new feature A"
        $result.Sections["Features Added"] | Should -Contain "- Added new feature B"
    }

    It "Should replace existing content in changelog entry" {
        # Create a mock changelog entry with existing content
        $entry = [PSCustomObject]@{
            ReleaseVersion = "1.0.0"
            ReleaseStatus = "(Unreleased)"
            ReleaseTitle = "## 1.0.0 (Unreleased)"
            ReleaseContent = @("", "### Old Section", "", "- Old content")
            Sections = @{
                "Old Section" = @("", "- Old content")
            }
        }

        $newContent = "### Breaking Changes`n`n- New breaking change"

        $result = Set-ChangeLogEntryContent -ChangeLogEntry $entry -NewContent $newContent -InitialAtxHeader "#"

        $result | Should -Not -BeNullOrEmpty
        $result.Sections.ContainsKey("Breaking Changes") | Should -BeTrue
        $result.Sections.ContainsKey("Old Section") | Should -BeFalse
    }

    It "Should use default InitialAtxHeader when not specified" {
        $entry = [PSCustomObject]@{
            ReleaseVersion = "1.0.0"
            ReleaseStatus = "(Unreleased)"
            ReleaseTitle = "## 1.0.0 (Unreleased)"
            ReleaseContent = @()
            Sections = @{}
        }

        $newContent = "### Features Added`n`n- Feature 1"

        $result = Set-ChangeLogEntryContent -ChangeLogEntry $entry -NewContent $newContent

        $result | Should -Not -BeNullOrEmpty
        $result.Sections.ContainsKey("Features Added") | Should -BeTrue
    }
}

Describe "Integration: Update Changelog Entry and Write Back" {
    BeforeEach {
        # Create a temporary changelog file for integration testing
        $script:tempChangelogPath = Join-Path ([System.IO.Path]::GetTempPath()) "CHANGELOG_$([System.Guid]::NewGuid().ToString()).md"
        
        $initialChangelog = @"
# Release History

## 1.0.0 (Unreleased)

### Features Added

### Breaking Changes

### Bugs Fixed

## 0.9.0 (2024-01-15)

### Features Added

- Initial release feature
"@
        Set-Content -Path $script:tempChangelogPath -Value $initialChangelog
    }

    AfterEach {
        if (Test-Path $script:tempChangelogPath) {
            Remove-Item -Path $script:tempChangelogPath -Force -ErrorAction SilentlyContinue
        }
    }

    It "Should update changelog file through the full workflow" {
        # Get existing entries
        $entries = Get-ChangeLogEntries -ChangeLogLocation $script:tempChangelogPath
        $entries | Should -Not -BeNullOrEmpty

        # Get the unreleased entry
        $unreleasedEntry = $entries["1.0.0"]
        $unreleasedEntry | Should -Not -BeNullOrEmpty
        $unreleasedEntry.ReleaseStatus | Should -Be "(Unreleased)"

        # Prepare new content
        $newContent = "### Breaking Changes`n`n- Removed deprecated method ``oldApi()```n- Changed signature of ``processData()```n`n### Features Added`n`n- Added async support for all operations`n- Added new ``streamData()`` method"

        # Update the entry content
        Set-ChangeLogEntryContent -ChangeLogEntry $unreleasedEntry -NewContent $newContent -InitialAtxHeader $entries.InitialAtxHeader

        # Write back to file
        Set-ChangeLogContent -ChangeLogLocation $script:tempChangelogPath -ChangeLogEntries $entries

        # Verify the file was updated correctly
        $updatedContent = Get-Content -Path $script:tempChangelogPath -Raw
        $updatedContent | Should -Match "Removed deprecated method"
        $updatedContent | Should -Match "Added async support"
        $updatedContent | Should -Match "streamData"

        # Verify the structure is still valid
        $updatedEntries = Get-ChangeLogEntries -ChangeLogLocation $script:tempChangelogPath
        $updatedEntries | Should -Not -BeNullOrEmpty
        $updatedEntries["1.0.0"].Sections.ContainsKey("Breaking Changes") | Should -BeTrue
        $updatedEntries["1.0.0"].Sections.ContainsKey("Features Added") | Should -BeTrue
    }

    It "Should preserve other versions when updating one version" {
        $entries = Get-ChangeLogEntries -ChangeLogLocation $script:tempChangelogPath
        
        $unreleasedEntry = $entries["1.0.0"]
        $newContent = "### Features Added`n`n- New feature"
        
        Set-ChangeLogEntryContent -ChangeLogEntry $unreleasedEntry -NewContent $newContent -InitialAtxHeader $entries.InitialAtxHeader
        Set-ChangeLogContent -ChangeLogLocation $script:tempChangelogPath -ChangeLogEntries $entries

        # Verify the old version is still present
        $updatedContent = Get-Content -Path $script:tempChangelogPath -Raw
        $updatedContent | Should -Match "0.9.0"
        $updatedContent | Should -Match "Initial release feature"

        $updatedEntries = Get-ChangeLogEntries -ChangeLogLocation $script:tempChangelogPath
        $updatedEntries["0.9.0"] | Should -Not -BeNullOrEmpty
    }
}
