Import-Module Pester

BeforeAll {
    . $PSScriptRoot/../common/scripts/ChangeLog-Operations.ps1
}

Describe "Parse-ChangelogContent" {
    It "Should parse changelog text with multiple sections" {
        $changelogText = "### Breaking Changes`n`n- Removed deprecated API ``oldMethod()```n- Changed return type of ``getData()```n`n### Features Added`n`n- Added new ``newMethod()`` API`n- Added support for async operations`n`n### Bugs Fixed`n`n- Fixed null pointer exception in ``processData()``"

        $result = Parse-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "#"

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

        $result = Parse-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "#"

        $result | Should -Not -BeNullOrEmpty
        $result.Sections.Keys | Should -HaveCount 1
        $result.Sections.ContainsKey("Features Added") | Should -BeTrue
        $result.Sections["Features Added"] | Should -Contain ""
        $result.Sections["Features Added"] | Should -Contain "- Added new feature X"
        $result.Sections["Features Added"] | Should -Contain "- Added new feature Y"
    }

    It "Should handle empty changelog text sections with content before first section" {
        $changelogText = "Some introductory text`n`n### Breaking Changes`n`n- Change 1"

        $result = Parse-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "#"

        $result | Should -Not -BeNullOrEmpty
        $result.Sections.Keys | Should -HaveCount 1
        # The intro text should be in ReleaseContent but not in any section
        $result.ReleaseContent | Should -Contain "Some introductory text"
    }

    It "Should respect InitialAtxHeader parameter" {
        # With ## as initial header, section headers are ####
        $changelogText = "#### Breaking Changes`n`n- Some breaking change"

        $result = Parse-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "##"

        $result | Should -Not -BeNullOrEmpty
        $result.Sections.Keys | Should -HaveCount 1
        $result.Sections.ContainsKey("Breaking Changes") | Should -BeTrue
    }

    It "Should return empty sections when no section headers found" {
        $changelogText = "Just some text without any section headers`nAnd another line"

        $result = Parse-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "#"

        $result | Should -Not -BeNullOrEmpty
        $result.Sections.Keys | Should -HaveCount 0
        $result.ReleaseContent | Should -Not -BeNullOrEmpty
    }

    It "Should handle Windows-style line endings" {
        $changelogText = "### Features Added`r`n`r`n- Feature 1`r`n- Feature 2"

        $result = Parse-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "#"

        $result | Should -Not -BeNullOrEmpty
        $result.Sections.ContainsKey("Features Added") | Should -BeTrue
    }

    It "Should handle Unix-style line endings" {
        $changelogText = "### Features Added`n`n- Feature 1`n- Feature 2"

        $result = Parse-ChangelogContent -ChangelogText $changelogText -InitialAtxHeader "#"

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

Describe "Python post-release changelog parsing" {
    BeforeEach {
        $global:Language = "python"
    }

    AfterEach {
        $global:Language = ""
    }

    It "Should parse changelog with GA post-release version '1.0.0.post1'" {
        $changelogContent = @"
# Release History

## 1.0.0.post1 (2025-03-01)

### Other Changes

- Updated package metadata for distribution

## 1.0.0 (2025-02-15)

### Features Added

- Initial GA release
"@
        $entries = Get-ChangeLogEntriesFromContent $changelogContent
        $entries | Should -Not -BeNullOrEmpty
        $entries["1.0.0.post1"] | Should -Not -BeNullOrEmpty
        $entries["1.0.0.post1"].ReleaseVersion | Should -Be "1.0.0.post1"
        $entries["1.0.0.post1"].ReleaseStatus | Should -Be "(2025-03-01)"
        $entries["1.0.0.post1"].Sections.ContainsKey("Other Changes") | Should -BeTrue
        $entries["1.0.0"] | Should -Not -BeNullOrEmpty
    }

    It "Should parse changelog with beta post-release version '1.0.0b2.post1'" {
        $changelogContent = @"
# Release History

## 1.0.0b2.post1 (2025-03-01)

### Other Changes

- Updated classifier metadata for beta release

## 1.0.0b2 (2025-02-15)

### Features Added

- Beta feature
"@
        $entries = Get-ChangeLogEntriesFromContent $changelogContent
        $entries | Should -Not -BeNullOrEmpty
        $entries["1.0.0b2.post1"] | Should -Not -BeNullOrEmpty
        $entries["1.0.0b2.post1"].ReleaseVersion | Should -Be "1.0.0b2.post1"
        $entries["1.0.0b2.post1"].Sections.ContainsKey("Other Changes") | Should -BeTrue
        $entries["1.0.0b2"] | Should -Not -BeNullOrEmpty
    }

    It "Should parse changelog with alpha post-release version '2.0.0a20201208001.post2'" {
        $changelogContent = @"
# Release History

## 2.0.0a20201208001.post2 (2025-03-01)

### Other Changes

- Updated alpha package metadata

## 2.0.0a20201208001 (2025-02-15)

### Features Added

- Alpha feature
"@
        $entries = Get-ChangeLogEntriesFromContent $changelogContent
        $entries | Should -Not -BeNullOrEmpty
        $entries["2.0.0a20201208001.post2"] | Should -Not -BeNullOrEmpty
        $entries["2.0.0a20201208001.post2"].ReleaseVersion | Should -Be "2.0.0a20201208001.post2"
        $entries["2.0.0a20201208001"] | Should -Not -BeNullOrEmpty
    }

    It "Should parse changelog with unreleased post-release version" {
        $changelogContent = @"
# Release History

## 1.0.0.post2 (Unreleased)

### Other Changes

## 1.0.0.post1 (2025-02-15)

### Other Changes

- Updated package metadata
"@
        $entries = Get-ChangeLogEntriesFromContent $changelogContent
        $entries | Should -Not -BeNullOrEmpty
        $entries["1.0.0.post2"] | Should -Not -BeNullOrEmpty
        $entries["1.0.0.post2"].ReleaseStatus | Should -Be "(Unreleased)"
        $entries["1.0.0.post1"] | Should -Not -BeNullOrEmpty
    }

    It "Should parse changelog with multiple post-release versions" {
        $changelogContent = @"
# Release History

## 1.0.0.post3 (2025-04-01)

### Other Changes

- Updated package classifiers

## 1.0.0.post2 (2025-03-15)

### Other Changes

- Updated package description

## 1.0.0.post1 (2025-03-01)

### Other Changes

- Updated package metadata

## 1.0.0 (2025-02-15)

### Features Added

- Initial release
"@
        $entries = Get-ChangeLogEntriesFromContent $changelogContent
        $entries | Should -Not -BeNullOrEmpty
        $entries["1.0.0.post3"] | Should -Not -BeNullOrEmpty
        $entries["1.0.0.post2"] | Should -Not -BeNullOrEmpty
        $entries["1.0.0.post1"] | Should -Not -BeNullOrEmpty
        $entries["1.0.0"] | Should -Not -BeNullOrEmpty
    }
}

Describe "Python post-release changelog sorting" {
    BeforeEach {
        $global:Language = "python"
    }

    AfterEach {
        $global:Language = ""
    }

    It "Should sort post-release versions after their base version" {
        $changelogContent = @"
# Release History

## 1.0.0 (2025-02-15)

### Features Added

- Initial release

## 1.0.0.post1 (2025-03-01)

### Other Changes

- Updated package metadata
"@
        $entries = Get-ChangeLogEntriesFromContent $changelogContent
        $sorted = Sort-ChangeLogEntries -changeLogEntries $entries

        # post1 should come before (sort higher than) base 1.0.0 in descending sort
        $sortedVersions = @($sorted | ForEach-Object { $_.ReleaseVersion })
        $postIndex = [array]::IndexOf($sortedVersions, "1.0.0.post1")
        $baseIndex = [array]::IndexOf($sortedVersions, "1.0.0")
        $postIndex | Should -BeLessThan $baseIndex
    }
}

Describe "Python post-release changelog integration" {
    BeforeEach {
        $global:Language = "python"
        $script:tempChangelogPath = Join-Path ([System.IO.Path]::GetTempPath()) "CHANGELOG_$([System.Guid]::NewGuid().ToString()).md"
    }

    AfterEach {
        $global:Language = ""
        if (Test-Path $script:tempChangelogPath) {
            Remove-Item -Path $script:tempChangelogPath -Force -ErrorAction SilentlyContinue
        }
    }

    It "Should round-trip a changelog with post-release versions" {
        $initialChangelog = @"
# Release History

## 1.0.0.post1 (2025-03-01)

### Other Changes

- Updated package metadata

## 1.0.0 (2025-02-15)

### Features Added

- Initial release
"@
        Set-Content -Path $script:tempChangelogPath -Value $initialChangelog

        $entries = Get-ChangeLogEntries -ChangeLogLocation $script:tempChangelogPath
        $entries | Should -Not -BeNullOrEmpty
        $entries["1.0.0.post1"] | Should -Not -BeNullOrEmpty
        $entries["1.0.0"] | Should -Not -BeNullOrEmpty

        # Write back and re-read
        Set-ChangeLogContent -ChangeLogLocation $script:tempChangelogPath -ChangeLogEntries $entries

        $reReadEntries = Get-ChangeLogEntries -ChangeLogLocation $script:tempChangelogPath
        $reReadEntries["1.0.0.post1"] | Should -Not -BeNullOrEmpty
        $reReadEntries["1.0.0.post1"].ReleaseVersion | Should -Be "1.0.0.post1"
        $reReadEntries["1.0.0.post1"].ReleaseStatus | Should -Be "(2025-03-01)"
        $reReadEntries["1.0.0"] | Should -Not -BeNullOrEmpty
    }

    It "Should update a post-release changelog entry content" {
        $initialChangelog = @"
# Release History

## 1.0.0.post1 (Unreleased)

### Other Changes

## 1.0.0 (2025-02-15)

### Features Added

- Initial release
"@
        Set-Content -Path $script:tempChangelogPath -Value $initialChangelog

        $entries = Get-ChangeLogEntries -ChangeLogLocation $script:tempChangelogPath
        $postEntry = $entries["1.0.0.post1"]
        $postEntry | Should -Not -BeNullOrEmpty

        $newContent = "### Other Changes`n`n- Updated package metadata`n- Updated readme formatting"
        Set-ChangeLogEntryContent -ChangeLogEntry $postEntry -NewContent $newContent -InitialAtxHeader $entries.InitialAtxHeader
        Set-ChangeLogContent -ChangeLogLocation $script:tempChangelogPath -ChangeLogEntries $entries

        $updatedEntries = Get-ChangeLogEntries -ChangeLogLocation $script:tempChangelogPath
        $updatedEntries["1.0.0.post1"] | Should -Not -BeNullOrEmpty
        $updatedEntries["1.0.0.post1"].Sections.ContainsKey("Other Changes") | Should -BeTrue

        $updatedContent = Get-Content -Path $script:tempChangelogPath -Raw
        $updatedContent | Should -Match "Updated package metadata"
        $updatedContent | Should -Match "Updated readme formatting"

        # Verify the base version is preserved
        $updatedEntries["1.0.0"] | Should -Not -BeNullOrEmpty
    }

    It "Should preserve post-release entries when adding a new version" {
        $initialChangelog = @"
# Release History

## 1.0.0.post1 (2025-03-01)

### Other Changes

- Updated package metadata

## 1.0.0 (2025-02-15)

### Features Added

- Initial release
"@
        Set-Content -Path $script:tempChangelogPath -Value $initialChangelog

        $entries = Get-ChangeLogEntries -ChangeLogLocation $script:tempChangelogPath

        # Add a new version entry
        $newEntry = [pscustomobject]@{
            ReleaseVersion = "1.1.0"
            ReleaseStatus  = "(Unreleased)"
            ReleaseTitle   = "## 1.1.0 (Unreleased)"
            ReleaseContent = @("", "### Features Added", "", "- New feature for 1.1.0")
            Sections = @{ "Features Added" = @("", "- New feature for 1.1.0") }
        }
        $entries["1.1.0"] = $newEntry

        Set-ChangeLogContent -ChangeLogLocation $script:tempChangelogPath -ChangeLogEntries $entries

        $updatedEntries = Get-ChangeLogEntries -ChangeLogLocation $script:tempChangelogPath
        $updatedEntries["1.1.0"] | Should -Not -BeNullOrEmpty
        $updatedEntries["1.0.0.post1"] | Should -Not -BeNullOrEmpty
        $updatedEntries["1.0.0"] | Should -Not -BeNullOrEmpty
    }
}
