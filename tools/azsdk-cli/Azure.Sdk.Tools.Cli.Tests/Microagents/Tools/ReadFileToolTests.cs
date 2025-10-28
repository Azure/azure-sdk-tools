using Azure.Sdk.Tools.Cli.Microagents.Tools;

namespace Azure.Sdk.Tools.Cli.Tests.Microagents.Tools;

internal class ReadFileToolTests
{
    private string baseDir = string.Empty;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        baseDir = Path.Combine(Path.GetTempPath(), "readfile_" + Guid.NewGuid());
        Directory.CreateDirectory(baseDir);

        // Create a sample file with content
        File.WriteAllText(Path.Combine(baseDir, "sample.txt"), "Hello World\nSecond Line");
        Directory.CreateDirectory(Path.Combine(baseDir, "adir"));
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Test]
    public async Task ReadFile_FileExists_ReturnsContent()
    {
        // Arrange
        var tool = new ReadFileTool(baseDir);

        // Act
        var result = await tool.Invoke(new ReadFileInput("sample.txt"), CancellationToken.None);

        // Assert
        Assert.That(result.FileContent, Is.EqualTo("Hello World\nSecond Line"));
    }

    [Test]
    public void ReadFile_FileDoesNotExist_ThrowsArgumentException()
    {
        // Arrange
        var tool = new ReadFileTool(baseDir);

        // Act / Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await tool.Invoke(new ReadFileInput("missing.txt"), CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("does not exist"));
    }

    [Test]
    public void ReadFile_PathIsDirectory_ThrowsArgumentException()
    {
        // Arrange
        var tool = new ReadFileTool(baseDir);

        // Act / Assert (directories are treated as non-existent files by the tool)
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await tool.Invoke(new ReadFileInput("adir"), CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("does not exist"));
    }
}
