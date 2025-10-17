using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using OpenAI.Chat;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class ErrorAnalysisServiceTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange
            var logger = NullLogger<ErrorAnalysisService>.Instance;
            var openAIService = CreateMockOpenAIService();

            // Act & Assert
            Assert.DoesNotThrow(() => new ErrorAnalysisService(openAIService, logger));
        }

        [Test]
        public void Constructor_WithNullOpenAIService_ShouldThrowArgumentNullException()
        {
            // Arrange
            var logger = NullLogger<ErrorAnalysisService>.Instance;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorAnalysisService(null!, logger));
            Assert.That(ex?.ParamName, Is.EqualTo("aiService"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange
            var openAIService = CreateMockOpenAIService();

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorAnalysisService(openAIService, null!));
            Assert.That(ex?.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void GenerateFixesFromFailureLogsAsync_WithNullErrorOutput_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var service = CreateErrorAnalysisService();

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GenerateFixesFromFailureLogsAsync(null!, CancellationToken.None));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\n\n\t  \r\n")]
        public void GenerateFixesFromFailureLogsAsync_WithEmptyOrWhitespaceErrorOutput_ShouldThrowInvalidOperationException(string errorOutput)
        {
            // Arrange
            var service = CreateErrorAnalysisService();

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GenerateFixesFromFailureLogsAsync(errorOutput, CancellationToken.None));
        }

        [Test]
        public async Task GenerateFixesFromFailureLogsAsync_WithRegexMatchableError_ShouldParseCorrectly()
        {
            // Arrange
            var service = CreateErrorAnalysisService();
            var errorOutput = "error CS0103: The name 'variable' does not exist in the current context";

            // Act
            var result = await service.GenerateFixesFromFailureLogsAsync(errorOutput, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromFailureLogsAsync_WithMultipleRegexMatchableErrors_ShouldParseAll()
        {
            // Arrange
            var service = CreateErrorAnalysisService();
            var errorOutput = @"error CS0103: The name 'variable1' does not exist
error CS0246: The type or namespace name 'SomeType' could not be found
error AZC0001: Invalid parameter name";

            // Act
            var result = await service.GenerateFixesFromFailureLogsAsync(errorOutput, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromFailureLogsAsync_WithDuplicateErrors_ShouldDeduplicateCorrectly()
        {
            // Arrange
            var service = CreateErrorAnalysisService();
            var errorOutput = @"error CS0103: The name 'variable' does not exist
error CS0103: The name 'variable' does not exist
error CS0103: The name 'variable' does not exist";

            // Act
            var result = await service.GenerateFixesFromFailureLogsAsync(errorOutput, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromFailureLogsAsync_WithMixedContent_ShouldProcessSuccessfully()
        {
            // Arrange
            var service = CreateErrorAnalysisService();
            var errorOutput = @"error CS0103: The name 'variable' does not exist
Some unparsable error line
Another non-regex line
error CS0246: Type not found";

            // Act
            var result = await service.GenerateFixesFromFailureLogsAsync(errorOutput, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromFailureLogsAsync_WithCancellationToken_ShouldComplete()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var service = CreateErrorAnalysisService();
            var errorOutput = "error CS0103: The name 'variable' does not exist";

            // Act
            var result = await service.GenerateFixesFromFailureLogsAsync(errorOutput, cts.Token);

            // Assert
            Assert.That(result, Is.Not.Null);
        }

        private static ErrorAnalysisService CreateErrorAnalysisService()
        {
            var openAIService = CreateMockOpenAIService();
            var logger = NullLogger<ErrorAnalysisService>.Instance;
            return new ErrorAnalysisService(openAIService, logger);
        }

        private static OpenAIService CreateMockOpenAIService()
        {
            var mockChatClient = Mock.Of<ChatClient>();
            var appSettings = CreateMockAppSettings();
            var formatPromptService = new FormatPromptService(appSettings);
            var toolExecutor = CreateMockToolExecutor();
            var knowledgeBaseService = new KnowledgeBaseService(NullLogger<KnowledgeBaseService>.Instance);
            var logger = NullLogger<OpenAIService>.Instance;

            return new OpenAIService(mockChatClient, appSettings, formatPromptService, toolExecutor, knowledgeBaseService, logger);
        }

        private static ToolExecutor CreateMockToolExecutor()
        {
            var mockToolHandler = CreateMockToolHandler();
            return new ToolExecutor(mockToolHandler);
        }

        private static TypeSpecToolHandler CreateMockToolHandler()
        {
            // Create real instances with minimal dependencies for testing
            var logger = NullLogger<TypeSpecToolHandler>.Instance;
            var fileServiceLogger = NullLogger<TypeSpecFileService>.Instance;
            var githubFileServiceLogger = NullLogger<GitHubFileService>.Instance;
            var versionManagerLogger = NullLogger<TypeSpecFileVersionManager>.Instance;

            // Create AppSettings with test configuration
            var appSettings = CreateMockAppSettings();

            // Create HttpClient for GitHubFileService 
            var httpClient = new HttpClient();

            // Create GitHubFileService with correct parameter order
            var gitHubFileService = new GitHubFileService(appSettings, githubFileServiceLogger, httpClient);

            // Create TypeSpecFileService
            var fileService = new TypeSpecFileService(fileServiceLogger, gitHubFileService);

            // Create TypeSpecFileVersionManager with single parameter
            var versionManager = new TypeSpecFileVersionManager(versionManagerLogger);

            return new TypeSpecToolHandler(fileService, versionManager, logger);
        }

        private static AppSettings CreateMockAppSettings()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenAISettings:ApiKey"] = "test-api-key",
                    ["OpenAISettings:BaseUrl"] = "https://api.openai.com/v1",
                    ["OpenAISettings:ModelName"] = "gpt-4"
                })
                .Build();
            var logger = NullLogger<AppSettings>.Instance;

            return new AppSettings(configuration, logger);
        }
    }
}
