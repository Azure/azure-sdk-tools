using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using OpenAI.Chat;
using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Tools;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class ErrorAnalysisServiceTests
    {
        #region Constructor Tests

        [Test]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            var logger = NullLogger<ErrorAnalysisService>.Instance;
            var openAIService = CreateMockOpenAIService();
            var service = new ErrorAnalysisService(openAIService, logger);

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullOpenAIService_ShouldThrowArgumentNullException()
        {
            var logger = NullLogger<ErrorAnalysisService>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorAnalysisService(null!, logger));
            Assert.That(ex?.ParamName, Is.EqualTo("aiService"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var openAIService = CreateMockOpenAIService();
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorAnalysisService(openAIService, null!));
            Assert.That(ex?.ParamName, Is.EqualTo("logger"));
        }

        #endregion

        #region GenerateFixesFromResultAsync Tests

        [Test]
        public async Task GenerateFixesFromResultAsync_WithEmptyErrorOutput_ReturnsEmptyList()
        {
            var service = CreateErrorAnalysisService();
            var validationContext = CreateMockValidationContext();

            var result = await service.GenerateFixesFromResultAsync("", validationContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value, Is.Empty);
        }

        [Test]
        public async Task GenerateFixesFromResultAsync_WithSimpleErrorOutput_ReturnsExpectedResult()
        {
            var service = CreateErrorAnalysisService();
            var validationContext = CreateMockValidationContext();
            var errorOutput = "error CS0103: The name 'variable' does not exist";

            var result = await service.GenerateFixesFromResultAsync(errorOutput, validationContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
        }

        #endregion

        #region Helper Methods

        private ErrorAnalysisService CreateErrorAnalysisService(OpenAIService? openAIService = null, ILogger<ErrorAnalysisService>? logger = null)
        {
            var loggerInstance = logger ?? NullLogger<ErrorAnalysisService>.Instance;
            var aiService = openAIService ?? CreateMockOpenAIService();
            return new ErrorAnalysisService(aiService, loggerInstance);
        }

        private OpenAIService CreateMockOpenAIService()
        {
            var mockChatClient = Mock.Of<ChatClient>();
            var mockAppSettings = CreateMockAppSettings();
            var mockFormatPromptService = CreateMockFormatPromptService();
            
            // Create a proper mock for ToolExecutor with required constructor parameters
            var mockToolHandler = Mock.Of<ITypeSpecToolHandler>();
            var mockToolExecutorLogger = Mock.Of<ILogger<ToolExecutor>>();
            var mockToolExecutor = new ToolExecutor(mockToolHandler, mockToolExecutorLogger);
            
            var mockLogger = Mock.Of<ILogger<OpenAIService>>();

            return new OpenAIService(mockChatClient, mockAppSettings, mockFormatPromptService, mockToolExecutor, mockLogger);
        }

        private FormatPromptService CreateMockFormatPromptService()
        {
            var mockAppSettings = CreateMockAppSettings();
            return new FormatPromptService(mockAppSettings);
        }

        private ValidationContext CreateMockValidationContext()
        {
            // Use valid temp directory for testing
            var tempDir = Path.GetTempPath();
            return ValidationContext.ValidateAndCreate("specification/testservice/TestService", "abc123def456789abc123def456789abc123de", tempDir);
        }

        private AppSettings CreateMockAppSettings()
        {
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["OpenAI:ApiKey"]).Returns("test-api-key");
            mockConfig.Setup(c => c["OpenAI:ApiVersion"]).Returns("2024-02-15-preview");
            mockConfig.Setup(c => c["OpenAI:ModelName"]).Returns("gpt-4");
            mockConfig.Setup(c => c["GitHub:Owner"]).Returns("Azure");
            mockConfig.Setup(c => c["GitHub:Repository"]).Returns("azure-rest-api-specs");
            mockConfig.Setup(c => c["GitHub:CommitId"]).Returns("abc123");
            mockConfig.Setup(c => c["AzureOpenAI:Endpoint"]).Returns("https://test.openai.azure.com/");
            mockConfig.Setup(c => c["TypeSpec:Directory"]).Returns("specification/cognitiveservices/data-plane/Face");

            var mockLogger = Mock.Of<ILogger<AppSettings>>();
            return new AppSettings(mockConfig.Object, mockLogger);
        }

        #endregion
    }
}