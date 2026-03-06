using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.CopilotAgents;

internal class ToolHelpersTests
{
  private TempDirectory baseDir;

    [OneTimeSetUp]
  public void OneTimeSetup()
  {
    baseDir = TempDirectory.Create("base");
  }

    [OneTimeTearDown]
  public void OneTimeTearDown()
  {
    baseDir.Dispose();
  }

    [Test]
    public void TryGetSafeFullPath_ProvidesCorrectPath()
    {
        // Arrange
        var relativePath = $"test{Path.DirectorySeparatorChar}test.txt";

        // Act
        var result = ToolHelpers.TryGetSafeFullPath(baseDir.DirectoryPath, relativePath, out var fullPath);

        // Assert
        Assert.IsTrue(result);
        Assert.That(fullPath, Is.EqualTo(Path.Combine(baseDir.DirectoryPath, "test", "test.txt")));
    }

    [Test]
    public void TryGetSafeFullPath_WorksWithSingleDot()
    {
        // Arrange
        var relativePath = ".";

        // Act
        var result = ToolHelpers.TryGetSafeFullPath(baseDir.DirectoryPath, relativePath, out var fullPath);

        // Assert
        Assert.IsTrue(result);
        Assert.That(fullPath, Is.EqualTo(baseDir.DirectoryPath));
    }

    [Test]
    public void TryGetSafePath_RejectsFullPathOutsideOfPath()
    {
        // Arrange
      var relativePath = Path.Join(Path.GetPathRoot(Path.GetFullPath(baseDir.DirectoryPath)), "test.txt")!;

        // Act
      var result = ToolHelpers.TryGetSafeFullPath(baseDir.DirectoryPath, relativePath, out var _);

        // Assert
        Assert.IsFalse(result);
    }

    [Test]
    public void TryGetSafePath_RejectsPathThatResolvesOutsideOfBase()
    {
        // Arrange
        var relativePath = Path.Join("..", "test.txt");

        // Act
        var result = ToolHelpers.TryGetSafeFullPath(baseDir.DirectoryPath, relativePath, out var _);

        // Assert
        Assert.IsFalse(result);
    }
}
