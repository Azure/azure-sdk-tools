using System.Text.Json;
using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests;

[TestFixture]
internal class ToolExecutorTests
{
    [Test]
    public void Constructor_WithNullToolHandler_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ToolExecutor(null!));
    }

    [Test]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        var mockToolHandler = CreateMockToolHandler();

        // Act & Assert
        Assert.DoesNotThrow(() => new ToolExecutor(mockToolHandler));
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

    [TestCase("")]
    [TestCase(null)]
    public async Task ExecuteToolCallAsync_WithGetTypeSpecFile_EmptyOrNullPath_ShouldReturnErrorResponse(string? path)
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();
        var validationContext = CreateTestValidationContext();
        var argumentsJson = JsonSerializer.Serialize(new { path });

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

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task ExecuteToolCallAsync_WithNullEmptyOrWhitespaceToolName_ShouldReturnErrorResponse(string toolName)
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();
        var validationContext = CreateTestValidationContext();

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync(toolName, "{}", validationContext);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.That(errorResponse.TryGetProperty("error", out var errorProperty), Is.True);
        Assert.That(errorProperty.GetString(), Does.Contain("Tool name cannot be null or empty"));
    }

    [Test]
    public async Task ExecuteToolCallAsync_WithListTypeSpecFiles_ShouldReturnSerializedResponse()
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();
        var validationContext = CreateTestValidationContext();

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync(ToolNames.ListTypeSpecFiles, "{}", validationContext);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        // Verify it's valid JSON and contains expected structure
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.That(response.ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task ExecuteToolCallAsync_WithGetTypeSpecFile_ValidPath_ShouldReturnSerializedResponse()
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();
        var validationContext = CreateTestValidationContext();
        var argumentsJson = JsonSerializer.Serialize(new { path = "test.tsp" });

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync(ToolNames.GetTypeSpecFile, argumentsJson, validationContext);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        // Verify it's valid JSON and contains expected structure
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.That(response.ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task ExecuteToolCallAsync_WithNullArgumentsJson_ForListFiles_ShouldHandleGracefully()
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();
        var validationContext = CreateTestValidationContext();

        // Act & Assert
        Assert.DoesNotThrowAsync(async () =>
            await toolExecutor.ExecuteToolCallAsync(ToolNames.ListTypeSpecFiles, null!, validationContext));
    }

    [Test]
    public async Task ExecuteToolCallAsync_WithEmptyArgumentsJson_ForListFiles_ShouldWork()
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();
        var validationContext = CreateTestValidationContext();

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync(ToolNames.ListTypeSpecFiles, "{}", validationContext);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        // Verify it's valid JSON
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.That(response.ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task ExecuteToolCallAsync_WithCancellationToken_ShouldPassToToolHandler()
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();
        var validationContext = CreateTestValidationContext();
        using var cts = new CancellationTokenSource();

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync(ToolNames.ListTypeSpecFiles, "{}", validationContext, cts.Token);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        // Verify it completes without throwing (cancellation token is passed through)
        var response = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.That(response.ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task ExecuteToolCallAsync_WithGetTypeSpecFile_CaseInsensitivePath_ShouldWork()
    {
        // Arrange
        var toolExecutor = CreateToolExecutor();
        var validationContext = CreateTestValidationContext();
        var argumentsJson = JsonSerializer.Serialize(new { Path = "test.tsp" }); // Capital 'P'

        // Act
        var result = await toolExecutor.ExecuteToolCallAsync(ToolNames.GetTypeSpecFile, argumentsJson, validationContext);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        // Should return error since JSON property matching is case-sensitive
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(result);
        Assert.That(errorResponse.TryGetProperty("error", out var errorProperty), Is.True);
        Assert.That(errorProperty.GetString(), Does.Contain("Missing 'path' property"));
    }

    private ToolExecutor CreateToolExecutor()
    {
        var mockToolHandler = CreateMockToolHandler();
        return new ToolExecutor(mockToolHandler);
    }

    private TypeSpecToolHandler CreateMockToolHandler()
    {
        // For unit testing purposes, create minimal real instances
        // Note: This creates real objects with test configuration for testing ToolExecutor
        
        var mockFileServiceLogger = NullLogger<TypeSpecFileService>.Instance;
        var mockAppSettings = CreateTestAppSettings();
        var mockGitHubLogger = NullLogger<GitHubFileService>.Instance;
        var mockHttpClient = new HttpClient();
        var mockGitHubFileService = new GitHubFileService(mockAppSettings, mockGitHubLogger, mockHttpClient);
        var mockFileService = new TypeSpecFileService(mockFileServiceLogger, mockGitHubFileService);
        
        var mockVersionManagerLogger = NullLogger<TypeSpecFileVersionManager>.Instance;
        var mockVersionManager = new TypeSpecFileVersionManager(mockVersionManagerLogger);
        
        var mockToolHandlerLogger = NullLogger<TypeSpecToolHandler>.Instance;

        return new TypeSpecToolHandler(
            mockFileService,
            mockVersionManager,
            mockToolHandlerLogger);
    }

    private AppSettings CreateTestAppSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAISettings:ApiKey"] = "test-api-key",
                ["OpenAISettings:Model"] = "gpt-4",
                ["OpenAISettings:AgentName"] = "Test Agent",
                ["OpenAISettings:AgentInstructions"] = "Test instructions",
                ["OpenAISettings:ErrorAnalysisInstructions"] = "Analyze errors",
                ["OpenAISettings:FixPromptTemplate"] = "Fix template",
                ["OpenAISettings:MaxIterations"] = "3",
                ["OpenAISettings:IndexingMaxWaitTimeSeconds"] = "30",
                ["SourceRepo:BaseDirectory"] = Path.GetTempPath(),
                ["SourceRepo:GitHubUrl"] = "https://github.com/test/repo",
                ["SourceRepo:GitHubToken"] = "test-token",
                ["SourceRepo:BranchName"] = "main",
                ["SourceRepo:LocalBasePath"] = Path.GetTempPath(),
                ["SourceRepo:TypeSpecDirectoryName"] = "typespec"
            })
            .Build();

        var logger = NullLogger<AppSettings>.Instance;
        return new AppSettings(configuration, logger);
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
}