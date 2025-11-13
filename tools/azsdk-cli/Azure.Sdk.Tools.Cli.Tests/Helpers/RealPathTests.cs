// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class RealPathTests
{
    private TempDirectory _tempDir = null!;
    private string _originalDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _tempDir = TempDirectory.Create("RealPathTests");
        // Set working directory to temp dir so relative symlinks work correctly
        Directory.SetCurrentDirectory(_tempDir.DirectoryPath);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        _tempDir.Dispose();
    }

    #region Basic Path Resolution Tests

    [Test]
    public void GetRealPath_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => RealPath.GetRealPath(null!));
        Assert.That(ex.ParamName, Is.EqualTo("path"));
        Assert.That(ex.Message, Does.Contain("Path is null or empty"));
    }

    [Test]
    public void GetRealPath_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => RealPath.GetRealPath(""));
        Assert.That(ex.ParamName, Is.EqualTo("path"));
        Assert.That(ex.Message, Does.Contain("Path is null or empty"));
    }

    [Test]
    public void GetRealPath_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => RealPath.GetRealPath("   "));
        Assert.That(ex.ParamName, Is.EqualTo("path"));
        Assert.That(ex.Message, Does.Contain("Path is null or empty"));
    }

    [Test]
    public void GetRealPath_WithNonExistentPath_ReturnsNormalizedAbsolutePath()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDir.DirectoryPath, "does", "not", "exist");

        // Act
        var result = RealPath.GetRealPath(nonExistentPath);

        // Assert
        Assert.That(result, Is.EqualTo(Path.GetFullPath(nonExistentPath)));
    }

    [Test]
    public void GetRealPath_WithExistingFile_ReturnsAbsolutePath()
    {
        // Arrange
        var testFile = Path.Combine(_tempDir.DirectoryPath, "test.txt");
        File.WriteAllText(testFile, "test content");

        // Act
        var result = RealPath.GetRealPath(testFile);

        // Assert
        Assert.That(result, Is.EqualTo(Path.GetFullPath(testFile)));
        Assert.That(File.Exists(result), Is.True);
    }

    [Test]
    public void GetRealPath_WithExistingDirectory_ReturnsAbsolutePath()
    {
        // Arrange
        var testDir = Path.Combine(_tempDir.DirectoryPath, "testdir");
        Directory.CreateDirectory(testDir);

        // Act
        var result = RealPath.GetRealPath(testDir);

        // Assert
        Assert.That(result, Is.EqualTo(Path.GetFullPath(testDir)));
        Assert.That(Directory.Exists(result), Is.True);
    }

    [Test]
    public void GetRealPath_WithRelativePath_ResolvesToAbsolutePath()
    {
        // Arrange
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir.DirectoryPath);
            var testFile = Path.Combine(_tempDir.DirectoryPath, "test.txt");
            File.WriteAllText(testFile, "test content");

            // Act
            var result = RealPath.GetRealPath("test.txt");

            // Assert
            Assert.That(result, Is.EqualTo(Path.GetFullPath(testFile)));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    #endregion

    #region Symlink Tests

    [Test]
    public void GetRealPath_WithFileSymlink_ResolvesToTarget()
    {
        // Arrange - Create target and symlink in current directory (which is the temp dir)
        File.WriteAllText("target.txt", "target content");

        try
        {
            // Create symlink with relative target
            File.CreateSymbolicLink("symlink.txt", "target.txt");
        }
        catch (IOException)
        {
            Assert.Inconclusive("Symbolic links are not supported on this system");
            return;
        }

        // Act
        var result = RealPath.GetRealPath("symlink.txt");

        // Assert
        var expectedPath = Path.GetFullPath("target.txt");
        Assert.That(result, Is.EqualTo(expectedPath));
    }

    [Test]
    public void GetRealPath_WithDirectorySymlink_ResolvesToTarget()
    {
        // Arrange
        Directory.CreateDirectory("targetdir");

        try
        {
            Directory.CreateSymbolicLink("symlinkdir", "targetdir");
        }
        catch (IOException)
        {
            Assert.Inconclusive("Symbolic links are not supported on this system");
            return;
        }

        // Act
        var result = RealPath.GetRealPath("symlinkdir");

        // Assert
        Assert.That(result, Is.EqualTo(Path.GetFullPath("targetdir")));
    }

    [Test]
    public void GetRealPath_WithSymlinkChain_ResolvesToFinalTarget()
    {
        // Arrange
        File.WriteAllText("target.txt", "target content");

        try
        {
            File.CreateSymbolicLink("symlink1.txt", "target.txt");
            File.CreateSymbolicLink("symlink2.txt", "symlink1.txt");
        }
        catch (IOException)
        {
            Assert.Inconclusive("Symbolic links are not supported on this system");
            return;
        }

        // Act
        var result = RealPath.GetRealPath("symlink2.txt");

        // Assert
        Assert.That(result, Is.EqualTo(Path.GetFullPath("target.txt")));
    }

    [Test]
    public void GetRealPath_WithRelativeSymlink_ResolvesToAbsoluteTarget()
    {
        // Arrange
        Directory.CreateDirectory("subdir");
        File.WriteAllText("target.txt", "target content");

        try
        {
            // Create a relative symlink pointing to ../target.txt
            File.CreateSymbolicLink(Path.Combine("subdir", "symlink.txt"), Path.Combine("..", "target.txt"));
        }
        catch (IOException)
        {
            Assert.Inconclusive("Symbolic links are not supported on this system");
            return;
        }

        // Act
        var result = RealPath.GetRealPath(Path.Combine("subdir", "symlink.txt"));

        // Assert
        Assert.That(result, Is.EqualTo(Path.GetFullPath("target.txt")));
    }

    [Test]
    public void GetRealPath_WithSymlinkInPath_ResolvesToRealPath()
    {
        // Arrange
        Directory.CreateDirectory("targetdir");
        File.WriteAllText(Path.Combine("targetdir", "file.txt"), "content");

        try
        {
            Directory.CreateSymbolicLink("symlinkdir", "targetdir");
        }
        catch (IOException)
        {
            Assert.Inconclusive("Symbolic links are not supported on this system");
            return;
        }

        // Act
        var result = RealPath.GetRealPath(Path.Combine("symlinkdir", "file.txt"));

        // Assert
        Assert.That(result, Is.EqualTo(Path.GetFullPath(Path.Combine("targetdir", "file.txt"))));
    }

    [Test]
    public void GetRealPath_WithNonExistentFileInSymlinkedDir_ReturnsExpectedPath()
    {
        // Arrange
        Directory.CreateDirectory("targetdir");

        try
        {
            Directory.CreateSymbolicLink("symlinkdir", "targetdir");
        }
        catch (IOException)
        {
            Assert.Inconclusive("Symbolic links are not supported on this system");
            return;
        }

        // Act
        var result = RealPath.GetRealPath(Path.Combine("symlinkdir", "nonexistent.txt"));

        // Assert
        var expectedPath = Path.GetFullPath(Path.Combine("targetdir", "nonexistent.txt"));
        Assert.That(result, Is.EqualTo(expectedPath));
    }

    #endregion

    #region Junction Tests (Windows only)

    [Test]
    [Platform("Win")]
    public void GetRealPath_WithJunction_ResolvesToTarget()
    {
        // Arrange
        Directory.CreateDirectory("targetdir");

        try
        {
            Directory.CreateSymbolicLink("junctiondir", "targetdir");
        }
        catch (IOException)
        {
            Assert.Inconclusive("Junction creation is not supported on this system");
            return;
        }

        // Act
        var result = RealPath.GetRealPath("junctiondir");

        // Assert
        Assert.That(result, Is.EqualTo(Path.GetFullPath("targetdir")));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void GetRealPath_WithMaxDepthExceeded_ThrowsIOException()
    {
        // Arrange
        File.WriteAllText("target.txt", "target content");

        var symlinks = new List<string>();
        for (int i = 0; i < 70; i++)
        {
            symlinks.Add($"symlink{i}.txt");
        }

        try
        {
            File.CreateSymbolicLink(symlinks[0], "target.txt");
            for (int i = 1; i < symlinks.Count; i++)
            {
                File.CreateSymbolicLink(symlinks[i], $"symlink{i - 1}.txt");
            }
        }
        catch (IOException)
        {
            Assert.Inconclusive("Symbolic links are not supported on this system");
            return;
        }

        // Act & Assert
        var ex = Assert.Throws<IOException>(() => RealPath.GetRealPath(symlinks[symlinks.Count - 1]));
        // Different platforms / deep link chain failure modes can yield different messages.
        // Our implementation throws messages containing phrases like "Too many", "nested", "cycle", or "symbolic links".
        // On some Windows environments the OS resolves the chain first and returns a message like
        // "The name of the file cannot be resolved by the system." before our guard triggers.
        Assert.That(ex.Message, Is.Not.Null);
        Assert.That(
            ex.Message,
            Does.Contain("Too many")
                .Or.Contain("nested")
                .Or.Contain("cycle")
                .Or.Contain("symbolic links")
                .Or.Contain("cannot be resolved").IgnoreCase,
            () => $"Unexpected IOException message: '{ex.Message}'");
    }

    [Test]
    public void GetRealPath_WithDotSegments_ResolvesCorrectly()
    {
        // Arrange
        var subDir = Path.Combine(_tempDir.DirectoryPath, "subdir");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(_tempDir.DirectoryPath, "test.txt");
        File.WriteAllText(testFile, "content");

        var pathWithDots = Path.Combine(subDir, "..", "test.txt");

        // Act
        var result = RealPath.GetRealPath(pathWithDots);

        // Assert
        Assert.That(result, Is.EqualTo(Path.GetFullPath(testFile)));
    }

    [Test]
    public void GetRealPath_WithTrailingSlash_HandlesCorrectly()
    {
        // Arrange
        var testDir = Path.Combine(_tempDir.DirectoryPath, "testdir");
        Directory.CreateDirectory(testDir);
        var pathWithSlash = testDir + Path.DirectorySeparatorChar;

        // Act
        var result = RealPath.GetRealPath(pathWithSlash);

        // Assert
        Assert.That(Directory.Exists(result), Is.True);
        Assert.That(result, Is.EqualTo(Path.GetFullPath(testDir)));
    }

    #endregion

    #region Complex Scenarios

    [Test]
    public void GetRealPath_WithNestedSymlinksAndDirectories_ResolvesCorrectly()
    {
        // Arrange
        Directory.CreateDirectory("dir1");
        Directory.CreateDirectory(Path.Combine("dir2", "target"));
        File.WriteAllText(Path.Combine("dir2", "target", "file.txt"), "content");

        try
        {
            // Create relative symlink from dir1/symlink to dir2/target
            Directory.CreateSymbolicLink(Path.Combine("dir1", "symlink"), Path.Combine("..", "dir2", "target"));
        }
        catch (IOException)
        {
            Assert.Inconclusive("Symbolic links are not supported on this system");
            return;
        }

        // Act
        var result = RealPath.GetRealPath(Path.Combine("dir1", "symlink", "file.txt"));

        // Assert
        Assert.That(result, Is.EqualTo(Path.GetFullPath(Path.Combine("dir2", "target", "file.txt"))));
    }

    [Test]
    public void GetRealPath_WithSymlinkToSymlink_ResolvesToFinalTarget()
    {
        // Arrange
        Directory.CreateDirectory("target");
        File.WriteAllText(Path.Combine("target", "file.txt"), "content");

        try
        {
            Directory.CreateSymbolicLink("symlink1", "target");
            Directory.CreateSymbolicLink("symlink2", "symlink1");
        }
        catch (IOException)
        {
            Assert.Inconclusive("Symbolic links are not supported on this system");
            return;
        }

        // Act
        var result = RealPath.GetRealPath(Path.Combine("symlink2", "file.txt"));

        // Assert
        Assert.That(result, Is.EqualTo(Path.GetFullPath(Path.Combine("target", "file.txt"))));
    }

    #endregion
}
