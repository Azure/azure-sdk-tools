using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Constants;
using Azure.Tools.GeneratorAgent.Models;
using Azure.Tools.GeneratorAgent.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Text.Json;

namespace Azure.Tools.GeneratorAgent.Tests;

[TestFixture]
internal class ToolExecutorTests
{
    [Test]
    public void Constructor_WithNullToolHandler_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ToolExecutor>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ToolExecutor(null!, mockLogger.Object));
    }

    [Test]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockToolHandler = new Mock<ITypeSpecToolHandler>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ToolExecutor(mockToolHandler.Object, null!));
    }

    [Test]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        var mockToolHandler = new Mock<ITypeSpecToolHandler>();
        var mockLogger = new Mock<ILogger<ToolExecutor>>();

        // Act & Assert
        Assert.DoesNotThrow(() => new ToolExecutor(mockToolHandler.Object, mockLogger.Object));
    }

    [Test]
    public void ExecuteToolCallAsync_WithNullValidationContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => 
            toolExecutor.ExecuteToolCallAsync(ToolNames.ListTypeSpecFiles, "{}", null!, CancellationToken.None));
    }

    [Test]
    public async Task ExecuteToolCallAsync_WithListTypeSpecFiles_ShouldReturnSerializedResponse()
    {
        // Arrange
        var mockToolHandler = new Mock<ITypeSpecToolHandler>();
        var mockLogger = new Mock<ILogger<ToolExecutor>>();
        var validationContext = CreateTestValidationContext();
        
        var expectedResponse = new ListTypeSpecFilesResponse
        {
            Files = new List<TypeSpecFileInfo>
            {
                new() 
                { 
                    Path = "test.tsp", 
                    Lines = 10, 
                    Version = 1, 
                    Sha256 = "abcd1234",
                    Content = null
                }
            }
        };

        mockToolHandler.Setup(h => h.ListTypeSpecFilesAsync(validationContext, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(expectedResponse);

        var toolExecutor = new ToolExecutor(mockToolHandler.Object, mockLogger.Object);

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync(ToolNames.ListTypeSpecFiles, "{}", validationContext);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        var deserializedResult = JsonSerializer.Deserialize<ListTypeSpecFilesResponse>(result);
        Assert.That(deserializedResult, Is.Not.Null);
        Assert.That(deserializedResult!.Files, Has.Count.EqualTo(1));
        Assert.That(deserializedResult.Files[0].Path, Is.EqualTo("test.tsp"));
        
        mockToolHandler.Verify(h => h.ListTypeSpecFilesAsync(validationContext, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ExecuteToolCallAsync_WithGetTypeSpecFile_ValidPath_ShouldReturnSerializedResponse()
    {
        // Arrange
        var mockToolHandler = new Mock<ITypeSpecToolHandler>();
        var mockLogger = new Mock<ILogger<ToolExecutor>>();
        var validationContext = CreateTestValidationContext();
        
        var expectedResponse = new TypeSpecFileInfo
        {
            Path = "test.tsp",
            Lines = 20,
            Version = 1,
            Sha256 = "abcd1234",
            Content = "// Test content"
        };

        mockToolHandler.Setup(h => h.GetTypeSpecFileAsync("test.tsp", validationContext, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(expectedResponse);

        var toolExecutor = new ToolExecutor(mockToolHandler.Object, mockLogger.Object);
        var argumentsJson = JsonSerializer.Serialize(new { path = "test.tsp" });

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync(ToolNames.GetTypeSpecFile, argumentsJson, validationContext);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        var deserializedResult = JsonSerializer.Deserialize<TypeSpecFileInfo>(result);
        Assert.That(deserializedResult, Is.Not.Null);
        Assert.That(deserializedResult!.Path, Is.EqualTo("test.tsp"));
        Assert.That(deserializedResult.Content, Is.EqualTo("// Test content"));
        
        mockToolHandler.Verify(h => h.GetTypeSpecFileAsync("test.tsp", validationContext, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ExecuteToolCallAsync_WithGetTypeSpecFile_MissingPath_ShouldReturnErrorResponse()
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();
        var validationContext = CreateTestValidationContext();
        var argumentsJson = JsonSerializer.Serialize(new { });

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync(ToolNames.GetTypeSpecFile, argumentsJson, validationContext);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.That(errorResponse.TryGetProperty("error", out var errorProperty), Is.True);
        Assert.That(errorProperty.GetString(), Does.Contain("Missing 'path' property"));
    }

    [Test]
    public async Task ExecuteToolCallAsync_WithGetTypeSpecFile_EmptyPath_ShouldReturnErrorResponse()
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();
        var validationContext = CreateTestValidationContext();
        var argumentsJson = JsonSerializer.Serialize(new { path = "" });

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync(ToolNames.GetTypeSpecFile, argumentsJson, validationContext);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.That(errorResponse.TryGetProperty("error", out var errorProperty), Is.True);
        Assert.That(errorProperty.GetString(), Does.Contain("Missing or empty 'path'"));
    }

    [Test]
    public async Task ExecuteToolCallAsync_WithGetTypeSpecFile_NullPath_ShouldReturnErrorResponse()
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();
        var validationContext = CreateTestValidationContext();
        var argumentsJson = JsonSerializer.Serialize(new { path = (string?)null });

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync(ToolNames.GetTypeSpecFile, argumentsJson, validationContext);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.That(errorResponse.TryGetProperty("error", out var errorProperty), Is.True);
        Assert.That(errorProperty.GetString(), Does.Contain("Missing or empty 'path'"));
    }

    [Test]
    public async Task ExecuteToolCallAsync_WithUnknownTool_ShouldReturnErrorResponse()
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();
        var validationContext = CreateTestValidationContext();

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync("unknown_tool", "{}", validationContext);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.That(errorResponse.TryGetProperty("error", out var errorProperty), Is.True);
        Assert.That(errorProperty.GetString(), Does.Contain("Unknown tool: unknown_tool"));
    }

    [Test]
    public async Task ExecuteToolCallAsync_WhenToolHandlerThrows_ShouldReturnErrorResponseAndLogError()
    {
        // Arrange
        var mockToolHandler = new Mock<ITypeSpecToolHandler>();
        var mockLogger = new Mock<ILogger<ToolExecutor>>();
        var validationContext = CreateTestValidationContext();

        var expectedException = new InvalidOperationException("Tool handler error");
        mockToolHandler.Setup(h => h.ListTypeSpecFilesAsync(validationContext, It.IsAny<CancellationToken>()))
                      .ThrowsAsync(expectedException);

        var toolExecutor = new ToolExecutor(mockToolHandler.Object, mockLogger.Object);

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync(ToolNames.ListTypeSpecFiles, "{}", validationContext);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.That(errorResponse.TryGetProperty("error", out var errorProperty), Is.True);
        Assert.That(errorProperty.GetString(), Does.Contain("Tool execution failed: Tool handler error"));

        // Verify logging
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error executing tool")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task ExecuteToolCallAsync_WithInvalidJson_ShouldReturnErrorResponse()
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();
        var validationContext = CreateTestValidationContext();
        var invalidJson = "{ invalid json";

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync(ToolNames.GetTypeSpecFile, invalidJson, validationContext);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.That(errorResponse.TryGetProperty("error", out var errorProperty), Is.True);
        Assert.That(errorProperty.GetString(), Does.Contain("Tool execution failed"));
    }

    private ToolExecutor CreateToolExecutor()
    {
        var mockToolHandler = new Mock<ITypeSpecToolHandler>();
        var mockLogger = new Mock<ILogger<ToolExecutor>>();
        return new ToolExecutor(mockToolHandler.Object, mockLogger.Object);
    }

    private ValidationContext CreateTestValidationContext()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        // Create a test .tsp file
        var testTspFile = Path.Combine(tempDir, "test.tsp");
        File.WriteAllText(testTspFile, "// Test TypeSpec content");
        
        return ValidationContext.ValidateAndCreate(tempDir, null, Path.GetTempPath());
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up any temporary directories created during tests
        var tempPath = Path.GetTempPath();
        var tempDirs = Directory.GetDirectories(tempPath, "*", SearchOption.TopDirectoryOnly)
            .Where(d => Path.GetFileName(d).Length == 36); // GUID length
        
        foreach (var dir in tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}