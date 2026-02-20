// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class ChangelogHelperTests
{
    private TempDirectory _tempDir = null!;
    private ILogger<ChangelogHelper> _logger = null!;
    private ChangelogHelper _changelogHelper = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = TempDirectory.Create("ChangelogHelperTests");
        _logger = new TestLogger<ChangelogHelper>();
        _changelogHelper = new ChangelogHelper(_logger);
    }

    [TearDown]
    public void TearDown()
    {
        _tempDir.Dispose();
    }

    #region GetChangelogPath Tests

    [Test]
    public void GetChangelogPath_WhenChangelogExists_ReturnsPath()
    {
        // Arrange
        var changelogPath = Path.Combine(_tempDir.DirectoryPath, "CHANGELOG.md");
        File.WriteAllText(changelogPath, "# Release History");

        // Act
        var result = _changelogHelper.GetChangelogPath(_tempDir.DirectoryPath);

        // Assert
        Assert.That(result, Is.EqualTo(changelogPath));
    }

    [Test]
    public void GetChangelogPath_WhenChangelogDoesNotExist_ReturnsNull()
    {
        // Act
        var result = _changelogHelper.GetChangelogPath(_tempDir.DirectoryPath);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetChangelogPath_WhenPackagePathIsEmpty_ReturnsNull()
    {
        // Act
        var result = _changelogHelper.GetChangelogPath(string.Empty);

        // Assert
        Assert.That(result, Is.Null);
    }

    #endregion

    #region ParseChangelog Tests

    [Test]
    public void ParseChangelog_WithStandardFormat_ParsesCorrectly()
    {
        // Arrange
        var changelogContent = """
            # Release History

            ## 1.0.0 (2025-01-15)

            ### Features Added

            - Added feature A
            - Added feature B

            ## 1.0.0-beta.2 (2024-12-01)

            ### Breaking Changes

            - Renamed method X to Y

            ## 1.0.0-beta.1 (Unreleased)

            ### Features Added

            - Initial beta release
            """;
        var changelogPath = CreateChangelog(changelogContent);

        // Act
        var result = _changelogHelper.ParseChangelog(changelogPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.InitialAtxHeader, Is.EqualTo("#"));
        Assert.That(result.Entries, Has.Count.EqualTo(3));

        // Verify entries are in file order
        Assert.That(result.Entries[0].Version, Is.EqualTo("1.0.0"));
        Assert.That(result.Entries[0].ReleaseStatus, Is.EqualTo("(2025-01-15)"));
        Assert.That(result.Entries[1].Version, Is.EqualTo("1.0.0-beta.2"));
        Assert.That(result.Entries[1].ReleaseStatus, Is.EqualTo("(2024-12-01)"));
        Assert.That(result.Entries[2].Version, Is.EqualTo("1.0.0-beta.1"));
        Assert.That(result.Entries[2].ReleaseStatus, Is.EqualTo("(Unreleased)"));
    }

    [Test]
    public void ParseChangelog_WithTryGetEntry_FindsVersionCaseInsensitive()
    {
        // Arrange
        var changelogContent = """
            # Release History

            ## 1.0.0-Beta.1 (Unreleased)

            ### Features Added

            - Initial release
            """;
        var changelogPath = CreateChangelog(changelogContent);

        // Act
        var result = _changelogHelper.ParseChangelog(changelogPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TryGetEntry("1.0.0-beta.1", out var entry), Is.True);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Version, Is.EqualTo("1.0.0-Beta.1"));
    }

    [Test]
    public void ParseChangelog_PreservesHeaderBlock()
    {
        // Arrange
        var changelogContent = """
            # Release History

            This is a description of the package.
            More details here.

            ## 1.0.0 (2025-01-15)

            ### Features Added

            - Feature A
            """;
        var changelogPath = CreateChangelog(changelogContent);

        // Act
        var result = _changelogHelper.ParseChangelog(changelogPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.HeaderBlock, Does.Contain("This is a description"));
        Assert.That(result.HeaderBlock, Does.Contain("More details here"));
    }

    [Test]
    public void ParseChangelog_WithNonExistentFile_ReturnsNull()
    {
        // Act
        var result = _changelogHelper.ParseChangelog("/non/existent/path/CHANGELOG.md");

        // Assert
        Assert.That(result, Is.Null);
    }

    #endregion

    #region UpdateReleaseDate Tests

    [Test]
    public void UpdateReleaseDate_FromUnreleased_UpdatesCorrectly()
    {
        // Arrange
        var changelogContent = """
            # Release History

            ## 1.0.0 (Unreleased)

            ### Features Added

            - Added feature A
            - Added feature B

            ### Breaking Changes

            - Changed API signature
            """;
        var changelogPath = CreateChangelog(changelogContent);

        // Act
        var result = _changelogHelper.UpdateReleaseDate(changelogPath, "1.0.0", "2025-01-30");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Message, Does.Contain("Updated release date"));

        // Verify file was updated
        var updatedContent = File.ReadAllText(changelogPath);
        Assert.That(updatedContent, Does.Contain("## 1.0.0 (2025-01-30)"));
        Assert.That(updatedContent, Does.Not.Contain("(Unreleased)"));
    }

    [Test]
    public void UpdateReleaseDate_FromExistingDate_UpdatesToNewDate()
    {
        // Arrange
        var changelogContent = """
            # Release History

            ## 1.0.0 (2025-01-15)

            ### Features Added

            - Feature A
            """;
        var changelogPath = CreateChangelog(changelogContent);

        // Act
        var result = _changelogHelper.UpdateReleaseDate(changelogPath, "1.0.0", "2025-01-30");

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedContent = File.ReadAllText(changelogPath);
        Assert.That(updatedContent, Does.Contain("## 1.0.0 (2025-01-30)"));
        Assert.That(updatedContent, Does.Not.Contain("(2025-01-15)"));
    }

    [Test]
    public void UpdateReleaseDate_WhenAlreadySameDate_ReturnsSuccessNoChange()
    {
        // Arrange
        var changelogContent = """
            # Release History

            ## 1.0.0 (2025-01-30)

            ### Features Added

            - Feature A
            """;
        var changelogPath = CreateChangelog(changelogContent);
        var originalContent = File.ReadAllText(changelogPath);

        // Act
        var result = _changelogHelper.UpdateReleaseDate(changelogPath, "1.0.0", "2025-01-30");

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.Message, Does.Contain("No change needed"));
    }

    [Test]
    public void UpdateReleaseDate_WithVersionNotFound_ReturnsFailure()
    {
        // Arrange
        var changelogContent = """
            # Release History

            ## 1.0.0 (Unreleased)

            ### Features Added

            - Feature A
            """;
        var changelogPath = CreateChangelog(changelogContent);

        // Act
        var result = _changelogHelper.UpdateReleaseDate(changelogPath, "2.0.0", "2025-01-30");

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("No changelog entry found for version 2.0.0"));
    }

    [Test]
    public void UpdateReleaseDate_WithInvalidDateFormat_ReturnsFailure()
    {
        // Arrange
        var changelogContent = """
            # Release History

            ## 1.0.0 (Unreleased)

            ### Features Added

            - Feature A
            """;
        var changelogPath = CreateChangelog(changelogContent);

        // Act
        var result = _changelogHelper.UpdateReleaseDate(changelogPath, "1.0.0", "01-30-2025");

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("Invalid release date format"));
    }

    [Test]
    public void UpdateReleaseDate_WithNonExistentFile_ReturnsFailure()
    {
        // Act
        var result = _changelogHelper.UpdateReleaseDate("/non/existent/path/CHANGELOG.md", "1.0.0", "2025-01-30");

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("does not exist"));
    }

    [Test]
    public void UpdateReleaseDate_PreservesEntryOrder()
    {
        // Arrange - multiple entries
        var changelogContent = """
            # Release History

            ## 2.0.0 (Unreleased)

            ### Features Added

            - New major features

            ## 1.1.0 (2025-01-15)

            ### Features Added

            - Minor features

            ## 1.0.0 (2024-12-01)

            ### Features Added

            - Initial release
            """;
        var changelogPath = CreateChangelog(changelogContent);

        // Act
        var result = _changelogHelper.UpdateReleaseDate(changelogPath, "2.0.0", "2025-01-30");

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedContent = File.ReadAllText(changelogPath);
        var lines = updatedContent.Split('\n').Select(l => l.Trim()).ToList();

        // Find positions of version entries
        var v200Index = lines.FindIndex(l => l.Contains("## 2.0.0"));
        var v110Index = lines.FindIndex(l => l.Contains("## 1.1.0"));
        var v100Index = lines.FindIndex(l => l.Contains("## 1.0.0"));

        // Verify order is preserved (2.0.0 before 1.1.0 before 1.0.0)
        Assert.That(v200Index, Is.LessThan(v110Index), "2.0.0 should appear before 1.1.0");
        Assert.That(v110Index, Is.LessThan(v100Index), "1.1.0 should appear before 1.0.0");
    }

    [Test]
    public void UpdateReleaseDate_PreservesSurroundingContent()
    {
        // Arrange - entry with detailed content
        var changelogContent = """
            # Release History

            ## 1.0.0 (Unreleased)

            ### Features Added

            - Added `NewClass` for handling X
            - Added `NewMethod()` for processing Y
              - Supports option A
              - Supports option B

            ### Breaking Changes

            - Removed deprecated `OldMethod()`
            - Changed return type of `GetData()` from `string` to `DataResult`

            ### Bugs Fixed

            - Fixed issue #123: Memory leak in parser
            - Fixed issue #456: Race condition in async handler

            ### Other Changes

            - Updated dependencies
            - Improved documentation
            """;
        var changelogPath = CreateChangelog(changelogContent);

        // Act
        var result = _changelogHelper.UpdateReleaseDate(changelogPath, "1.0.0", "2025-01-30");

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedContent = File.ReadAllText(changelogPath);

        // Verify all content sections are preserved
        Assert.That(updatedContent, Does.Contain("### Features Added"));
        Assert.That(updatedContent, Does.Contain("### Breaking Changes"));
        Assert.That(updatedContent, Does.Contain("### Bugs Fixed"));
        Assert.That(updatedContent, Does.Contain("### Other Changes"));
        Assert.That(updatedContent, Does.Contain("Added `NewClass`"));
        Assert.That(updatedContent, Does.Contain("Supports option A"));
        Assert.That(updatedContent, Does.Contain("Fixed issue #123"));
        Assert.That(updatedContent, Does.Contain("Updated dependencies"));
    }

    [Test]
    public void UpdateReleaseDate_WithPrereleaseVersion_UpdatesCorrectly()
    {
        // Arrange
        var changelogContent = """
            # Release History

            ## 1.0.0-beta.3 (Unreleased)

            ### Features Added

            - Beta feature

            ## 1.0.0-beta.2 (2025-01-01)

            ### Features Added

            - Previous beta
            """;
        var changelogPath = CreateChangelog(changelogContent);

        // Act
        var result = _changelogHelper.UpdateReleaseDate(changelogPath, "1.0.0-beta.3", "2025-01-30");

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedContent = File.ReadAllText(changelogPath);
        Assert.That(updatedContent, Does.Contain("## 1.0.0-beta.3 (2025-01-30)"));
    }

    [Test]
    public void UpdateReleaseDate_PreservesHeaderDescription()
    {
        // Arrange
        var changelogContent = """
            # Release History

            All notable changes to this library will be documented in this file.

            The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

            ## 1.0.0 (Unreleased)

            ### Features Added

            - Initial release
            """;
        var changelogPath = CreateChangelog(changelogContent);

        // Act
        var result = _changelogHelper.UpdateReleaseDate(changelogPath, "1.0.0", "2025-01-30");

        // Assert
        Assert.That(result.Success, Is.True);

        var updatedContent = File.ReadAllText(changelogPath);
        Assert.That(updatedContent, Does.Contain("All notable changes"));
        Assert.That(updatedContent, Does.Contain("Keep a Changelog"));
    }

    #endregion

    #region ChangelogData.TryGetEntry Tests

    [Test]
    public void TryGetEntry_WithExistingVersion_ReturnsTrue()
    {
        // Arrange
        var changelogContent = """
            # Release History

            ## 1.0.0 (Unreleased)

            ### Features Added

            - Feature A
            """;
        var changelogPath = CreateChangelog(changelogContent);
        var data = _changelogHelper.ParseChangelog(changelogPath);

        // Act
        var found = data!.TryGetEntry("1.0.0", out var entry);

        // Assert
        Assert.That(found, Is.True);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Version, Is.EqualTo("1.0.0"));
    }

    [Test]
    public void TryGetEntry_WithNonExistingVersion_ReturnsFalse()
    {
        // Arrange
        var changelogContent = """
            # Release History

            ## 1.0.0 (Unreleased)

            ### Features Added

            - Feature A
            """;
        var changelogPath = CreateChangelog(changelogContent);
        var data = _changelogHelper.ParseChangelog(changelogPath);

        // Act
        var found = data!.TryGetEntry("2.0.0", out var entry);

        // Assert
        Assert.That(found, Is.False);
        Assert.That(entry, Is.Null);
    }

    [Test]
    public void TryGetEntry_IsCaseInsensitive()
    {
        // Arrange
        var changelogContent = """
            # Release History

            ## 1.0.0-BETA.1 (Unreleased)

            ### Features Added

            - Feature A
            """;
        var changelogPath = CreateChangelog(changelogContent);
        var data = _changelogHelper.ParseChangelog(changelogPath);

        // Act & Assert - all case variations should work
        Assert.That(data!.TryGetEntry("1.0.0-beta.1", out _), Is.True);
        Assert.That(data!.TryGetEntry("1.0.0-BETA.1", out _), Is.True);
        Assert.That(data!.TryGetEntry("1.0.0-Beta.1", out _), Is.True);
    }

    #endregion

    #region Helper Methods

    private string CreateChangelog(string content)
    {
        var changelogPath = Path.Combine(_tempDir.DirectoryPath, "CHANGELOG.md");
        File.WriteAllText(changelogPath, content);
        return changelogPath;
    }

    #endregion
}
