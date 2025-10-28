using Azure.Sdk.Tools.Cli.Microagents;

namespace Azure.Sdk.Tools.Cli.Tests.Microagents;

internal class ToolHelpersTests
{

    private string baseDir;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        baseDir = Path.Combine(Path.GetTempPath(), "base_" + Guid.NewGuid());
        Directory.CreateDirectory(baseDir);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (Directory.Exists(baseDir))
        {
            Directory.Delete(baseDir, true);
        }
    }

    private class TestJsonSchemaObject
    {
        [System.ComponentModel.Description("Test description")]
        public required string Name { get; set; }

        public required string Description { get; set; }

        public string? OptionalProperty { get; set; }

        public bool AnotherProperty { get; set; }
    }

    [Test]
    public void GetJsonSchemaRepresentation_CreatesCorrectSchema()
    {

        // Arrange
        var expectedSchema = """
        {
          "type": "object",
          "properties": {
            "Name": {
              "description": "Test description",
              "type": "string"
            },
            "Description": {
              "type": "string"
            },
            "OptionalProperty": {
              "type": [
                "string",
                "null"
              ]
            },
            "AnotherProperty": {
              "type": "boolean"
            }
          },
          "required": [
            "Name",
            "Description"
          ]
        }
        """;

        // Act
        var schema = ToolHelpers.GetJsonSchemaRepresentation(typeof(TestJsonSchemaObject));

        // Assert
        Assert.That(schema, Is.EqualTo(expectedSchema));
    }

    [Test]
    public void TryGetSafeFullPath_ProvidesCorrectPath()
    {
        // Arrange
        var relativePath = $"test{Path.DirectorySeparatorChar}test.txt";

        // Act
        var result = ToolHelpers.TryGetSafeFullPath(baseDir, relativePath, out var fullPath);

        // Assert
        Assert.IsTrue(result);
        Assert.That(fullPath, Is.EqualTo(Path.Combine(baseDir, "test", "test.txt")));
    }

    [Test]
    public void TryGetSafeFullPath_WorksWithSingleDot()
    {
        // Arrange
        var relativePath = ".";

        // Act
        var result = ToolHelpers.TryGetSafeFullPath(baseDir, relativePath, out var fullPath);

        // Assert
        Assert.IsTrue(result);
        Assert.That(fullPath, Is.EqualTo(baseDir));
    }

    [Test]
    public void TryGetSafePath_RejectsFullPathOutsideOfPath()
    {
        // Arrange
        var relativePath = Path.Join(Path.GetPathRoot(Path.GetFullPath(baseDir)), "test.txt")!;

        // Act
        var result = ToolHelpers.TryGetSafeFullPath(baseDir, relativePath, out var _);

        // Assert
        Assert.IsFalse(result);
    }

    [Test]
    public void TryGetSafePath_RejectsPathThatResolvesOutsideOfBase()
    {
        // Arrange
        var relativePath = Path.Join("..", "test.txt");

        // Act
        var result = ToolHelpers.TryGetSafeFullPath(baseDir, relativePath, out var _);

        // Assert
        Assert.IsFalse(result);
    }
}
