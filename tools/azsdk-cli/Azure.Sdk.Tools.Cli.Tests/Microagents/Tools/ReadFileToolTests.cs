using Azure.Sdk.Tools.Cli.Microagents.Tools;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Microagents.Tools;

internal class ReadFileToolTests
{
    private TempDirectory? _temp;
    private string baseDir => _temp!.DirectoryPath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _temp = TempDirectory.Create("readfiletests");
        // Create a sample file with content
        File.WriteAllText(Path.Combine(baseDir, "sample.txt"), "Hello World\nSecond Line");
        Directory.CreateDirectory(Path.Combine(baseDir, "adir"));
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _temp?.Dispose();
    }

    [Test]
    public async Task ReadFile_FileExists_ReturnsContent()
    {
        // Arrange
        var tool = new ReadFileTool(baseDir);

        // Act
        var result = await tool.Invoke(new ReadFileInput("sample.txt"), CancellationToken.None);

        // Assert - ReadFileTool now returns line-numbered content for LLM patch tools
        Assert.That(result.FileContent, Does.Contain("1: Hello World"));
        Assert.That(result.FileContent, Does.Contain("2: Second Line"));
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
