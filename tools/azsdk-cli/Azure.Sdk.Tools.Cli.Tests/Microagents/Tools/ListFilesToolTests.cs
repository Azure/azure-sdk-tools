using Azure.Sdk.Tools.Cli.Microagents.Tools;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Microagents.Tools;

internal class ListFilesToolTests
{
    private TempDirectory? _temp;
    private string baseDir => _temp!.DirectoryPath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _temp = TempDirectory.Create("listfilestests");

        // Directory structure:
        // baseDir/
        //   rootfile.txt
        //   subdir/
        //     file1.txt
        //     file2.log
        //     nested/
        //       deep.txt
        File.WriteAllText(Path.Combine(baseDir, "rootfile.txt"), "root");
        var subdir = Path.Combine(baseDir, "subdir");
        Directory.CreateDirectory(subdir);
        File.WriteAllText(Path.Combine(subdir, "file1.txt"), "file1");
        File.WriteAllText(Path.Combine(subdir, "file2.log"), "file2");
        var nested = Path.Combine(subdir, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "deep.txt"), "deep");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _temp?.Dispose();
    }

    [Test]
    public async Task ListFiles_NonRecursive_ReturnsTopLevelEntries()
    {
        // Arrange
        var tool = new ListFilesTool(baseDir);

        // Act
        var result = await tool.Invoke(new ListFilesInput(Path: ".", Recursive: false, Filter: null), CancellationToken.None);

        // Assert - Expect directories AND files at top level
        var expected = new[] { "rootfile.txt", "subdir" };
        Assert.That(result.fileNames, Is.EquivalentTo(expected));
    }

    [Test]
    public async Task ListFiles_Recursive_ReturnsAllEntries()
    {
        // Arrange
        var tool = new ListFilesTool(baseDir);

        // Act
        var result = await tool.Invoke(new ListFilesInput(Path: ".", Recursive: true, Filter: null), CancellationToken.None);

        // Assert - Expect all files and directories
        var expected = new[]
        {
            "rootfile.txt",
            "subdir",
            Path.Join("subdir", "file1.txt"),
            Path.Join("subdir", "file2.log"),
            Path.Join("subdir", "nested"),
            Path.Join("subdir", "nested", "deep.txt"),
        };
        Assert.That(result.fileNames, Is.EquivalentTo(expected));
    }

    [Test]
    public async Task ListFiles_FilterNonRecursive_FiltersFiles()
    {
        // Arrange
        var tool = new ListFilesTool(baseDir);

        // Act
        var result = await tool.Invoke(new ListFilesInput(Path: ".", Recursive: false, Filter: "*.txt"), CancellationToken.None);

        // Assert - Only txt files at top level should be returned
        var expected = new[] { "rootfile.txt" };
        Assert.That(result.fileNames, Is.EquivalentTo(expected));
    }

    [Test]
    public async Task ListFiles_FilterRecursive_FiltersFilesRecursively()
    {
        // Arrange
        var tool = new ListFilesTool(baseDir);

        // Act
        var result = await tool.Invoke(new ListFilesInput(Path: ".", Recursive: true, Filter: "*.txt"), CancellationToken.None);

        // Assert - All txt files across tree
        var expected = new[]
        {
            "rootfile.txt",
            Path.Join("subdir", "file1.txt"),
            Path.Join("subdir", "nested", "deep.txt"),
        };
        Assert.That(result.fileNames, Is.EquivalentTo(expected));
    }

    [Test]
    public void ListFiles_PathDoesNotExist_ThrowsArgumentException()
    {
        // Arrange
        var tool = new ListFilesTool(baseDir);

        // Act / Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await tool.Invoke(new ListFilesInput(Path: "does_not_exist", Recursive: false, Filter: null), CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("does not exist"));
        Assert.That(ex.ParamName, Is.EqualTo("Path"));
    }

    [Test]
    public void ListFiles_PathIsFile_ThrowsArgumentException()
    {
        // Arrange
        var tool = new ListFilesTool(baseDir);

        // Act / Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await tool.Invoke(new ListFilesInput(Path: "rootfile.txt", Recursive: false, Filter: null), CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("is not a directory"));
        Assert.That(ex.ParamName, Is.EqualTo("Path"));
    }
}

