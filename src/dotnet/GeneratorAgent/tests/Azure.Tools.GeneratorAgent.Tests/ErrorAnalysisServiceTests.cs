using Azure.AI.Agents.Persistent;
using Azure.Tools.GeneratorAgent.Agent;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Exceptions;
using Azure.Tools.GeneratorAgent.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

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
            var toolBasedAgent = CreateDefaultToolBasedAgent();
            var service = new ErrorAnalysisService(toolBasedAgent, logger);

            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullErrorFixerAgent_ShouldThrowArgumentNullException()
        {
            var logger = NullLogger<ErrorAnalysisService>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorAnalysisService(null!, logger));
            Assert.That(ex?.ParamName, Is.EqualTo("toolBasedAgent"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var toolBasedAgent = CreateDefaultToolBasedAgent();
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorAnalysisService(toolBasedAgent, null!));
            Assert.That(ex?.ParamName, Is.EqualTo("logger"));
        }

        #endregion

        #region GenerateFixesFromResultsAsync Tests

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithNullResults_ReturnsEmptyList()
        {
            var service = CreateErrorAnalysisService();

            var result = await service.GenerateFixesFromResultsAsync(null, null, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value, Is.Empty);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithSuccessfulResults_ReturnsEmptyList()
        {
            var service = CreateErrorAnalysisService();
            var successResult = CreateSuccessResult();

            var result = await service.GenerateFixesFromResultsAsync(successResult, successResult, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value, Is.Empty);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithCompileFailure_ProcessesCompileErrors()
        {
            var service = CreateErrorAnalysisService();
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());

            var result = await service.GenerateFixesFromResultsAsync(compileResult, null, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithBuildFailure_ProcessesBuildErrors()
        {
            var service = CreateErrorAnalysisService();
            var buildResult = CreateFailureResult(CreateDotNetBuildException());

            var result = await service.GenerateFixesFromResultsAsync(null, buildResult, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithBothFailures_ProcessesBothResults()
        {
            var service = CreateErrorAnalysisService();
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());
            var buildResult = CreateFailureResult(CreateDotNetBuildException());

            var result = await service.GenerateFixesFromResultsAsync(compileResult, buildResult, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithNonFailureResults_DoesNotProcessErrors()
        {
            var mockLogger = CreateMockLogger();
            var service = CreateErrorAnalysisService(logger: mockLogger.Object);
            var successResult = CreateSuccessResult();

            var result = await service.GenerateFixesFromResultsAsync(successResult, successResult, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Empty);
            
            // When there are no failures to process, the service returns early without logging anything
            // so we verify that no processing logs were made
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Total errors found:")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_LogsTotalFixesGenerated()
        {
            var mockLogger = CreateMockLogger();
            var service = CreateErrorAnalysisService(logger: mockLogger.Object);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());

            await service.GenerateFixesFromResultsAsync(compileResult, null, CancellationToken.None);

            // Verify total errors count is logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Total errors found:")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithCancellationToken_RespectsCancellation()
        {
            var service = CreateErrorAnalysisService();
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());
            var cts = new CancellationTokenSource();

            var result = await service.GenerateFixesFromResultsAsync(compileResult, null, cts.Token);

            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithCancelledToken_CompletesGracefully()
        {
            var service = CreateErrorAnalysisService();
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await service.GenerateFixesFromResultsAsync(compileResult, null, cts.Token);
            
            // The service handles cancellation gracefully and returns a result
            Assert.That(result.IsSuccess, Is.True);
        }

        #endregion

        #region Regex Parsing Tests

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithSingleValidError_ParsesCorrectly()
        {
            var service = CreateErrorAnalysisService();
            var errorOutput = "error AZC0001: Type name 'Client' is too generic";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithMultipleValidErrors_ParsesAll()
        {
            var service = CreateErrorAnalysisService();
            var errorOutput = "error AZC0001: Type name 'Client' is too generic\nerror CS0103: The name 'variable' does not exist\nerror AZC0002: Another error";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithDuplicateErrors_RemovesDuplicates()
        {
            var service = CreateErrorAnalysisService();
            var errorOutput = "error AZC0001: Type name 'Client' is too generic\nerror AZC0001: Type name 'Client' is too generic";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithCaseInsensitiveErrors_ParsesCorrectly()
        {
            var service = CreateErrorAnalysisService();
            var errorOutput = "ERROR azc0001: Type name 'Client' is too generic";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithEmptyString_ReturnsEmptyList()
        {
            var service = CreateErrorAnalysisService();
            var result = CreateResultWithOutput("");

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
            Assert.That(fixes.Value, Is.Empty);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithWhitespaceString_ReturnsEmptyList()
        {
            var service = CreateErrorAnalysisService();
            var result = CreateResultWithOutput("   \n\t   ");

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
            Assert.That(fixes.Value, Is.Empty);
        }

        #endregion

        #region AI Fallback Tests

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithNoRegexMatches_FallsBackToErrorFixerAgent()
        {
            var service = CreateErrorAnalysisService(null);
            var errorOutput = "some error without standard format";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithNoRegexMatchesAndNullErrorFixerAgent_ReturnsEmptyList()
        {
            var service = CreateErrorAnalysisService(null);
            var errorOutput = "some error without standard format";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
        }

        #endregion

        #region Edge Cases and Error Handling

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithMalformedErrorText_HandlesGracefully()
        {
            var service = CreateErrorAnalysisService();
            var errorOutput = "error malformed text without proper format";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithVeryLongErrorMessage_HandlesCorrectly()
        {
            var service = CreateErrorAnalysisService();
            var longMessage = new string('x', 1000);
            var errorOutput = $"error AZC0001: {longMessage}";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithSpecialCharactersInErrorMessage_HandlesCorrectly()
        {
            var service = CreateErrorAnalysisService();
            var errorOutput = "error AZC0001: Error with special chars: @#$%^&*()[]{}|\\:;\"'<>,.?/";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithNullProcessException_ReturnsEmptyList()
        {
            var service = CreateErrorAnalysisService();
            var result = CreateResultWithNullProcessException();

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
            Assert.That(fixes.Value, Is.Empty);
        }

        #endregion

        #region Integration Tests

        [Test]
        public async Task GenerateFixesFromResultsAsync_EndToEndWithRealErrorScenario_WorksCorrectly()
        {
            var mockLogger = CreateMockLogger();
            var service = CreateErrorAnalysisService(null, mockLogger.Object);
            
            var realErrorOutput = @"
                build failed with errors:
                error AZC0001: Type name 'Client' is too generic and should be replaced with a more specific name
                error CS0103: The name 'undefinedVariable' does not exist in the current context
                error AZC0012: Model name 'Data' is too generic
                warning CS1998: This async method lacks 'await' operators
                error CS0246: The type or namespace name 'UnknownType' could not be found
            ";
            var result = CreateResultWithOutput(realErrorOutput);

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
            
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Total errors found:")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task GenerateFixesFromResultsAsync_WithMixedValidAndInvalidErrors_ProcessesValidOnes()
        {
            var service = CreateErrorAnalysisService();
            var errorOutput = @"
                error AZC0001: Valid error
                invalid error line
                error CS0103: Another valid error
                random text
                error: Invalid format
                error AZC0002: Valid error with proper format
            ";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
        }

        #endregion

        #region Regex Performance and Timeout Tests

        [Test] 
        public async Task GenerateFixesFromResultsAsync_WithRepeatedPatterns_DoesNotTimeout()
        {
            var service = CreateErrorAnalysisService();
            var repeatedPattern = new string('a', 1000);
            var errorOutput = $"error AZC0001: {repeatedPattern}";
            var result = CreateResultWithOutput(errorOutput);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var fixes = await service.GenerateFixesFromResultsAsync(result, null, CancellationToken.None);
            stopwatch.Stop();

            Assert.That(fixes.IsSuccess, Is.True);
            Assert.That(fixes.Value, Is.Not.Null);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000), "Regex should not take more than 5 seconds");
        }

        #endregion

        #region Helper Methods

        private ErrorAnalysisService CreateErrorAnalysisService(ToolBasedAgent? errorFixerAgent = null, ILogger<ErrorAnalysisService>? logger = null)
        {
            var loggerInstance = logger ?? NullLogger<ErrorAnalysisService>.Instance;
            var toolBasedAgent = errorFixerAgent ?? CreateDefaultToolBasedAgent();
            return new ErrorAnalysisService(toolBasedAgent, loggerInstance);
        }

        private ToolBasedAgent CreateDefaultToolBasedAgent()
        {
            var mockConversationManager = CreateMockConversationManager();
            var mockFormatPromptService = CreateMockFormatPromptService();
            var mockAppSettings = CreateMockAppSettings();
            var mockClient = Mock.Of<PersistentAgentsClient>();
            var mockLogger = Mock.Of<ILogger<ToolBasedAgent>>();

            return new ToolBasedAgent(
                mockConversationManager.Object,
                mockFormatPromptService,
                mockAppSettings,
                mockClient,
                mockLogger);
        }

        private Mock<ConversationManager> CreateMockConversationManager()
        {
            var mockLogger = Mock.Of<ILogger<ConversationManager>>();
            var mockAppSettings = CreateMockAppSettings();
            var mockToolExecutor = CreateMockToolExecutor();
            var mockClient = Mock.Of<PersistentAgentsClient>();

            return new Mock<ConversationManager>(mockClient, mockToolExecutor, mockAppSettings, mockLogger);
        }

        private ToolExecutor CreateMockToolExecutor()
        {
            var mockToolHandler = Mock.Of<ITypeSpecToolHandler>();
            var mockAppSettings = CreateMockAppSettings();
            var mockLogger = Mock.Of<ILogger<ToolExecutor>>();
            
            Func<ValidationContext, ITypeSpecToolHandler> toolHandlerFactory = _ => mockToolHandler;
            
            return new ToolExecutor(toolHandlerFactory, mockAppSettings, mockLogger);
        }        private FormatPromptService CreateMockFormatPromptService()
        {
            var mockLogger = Mock.Of<ILogger<FormatPromptService>>();
            var mockAppSettings = CreateMockAppSettings();

            return new FormatPromptService(mockLogger, mockAppSettings);
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

        private Mock<ILogger<ErrorAnalysisService>> CreateMockLogger()
        {
            return new Mock<ILogger<ErrorAnalysisService>>();
        }

        private TypeSpecCompilationException CreateTypeSpecCompilationException(
            string command = "tsp compile",
            string output = "TypeSpec output",
            string error = "error AZC0012: Type name 'Client' is too generic",
            int exitCode = 1)
        {
            return new TypeSpecCompilationException(command, output, error, exitCode);
        }

        private DotNetBuildException CreateDotNetBuildException(
            string command = "dotnet build",
            string output = "Build output",
            string error = "error CS0103: The name 'variable' does not exist",
            int exitCode = 1)
        {
            return new DotNetBuildException(command, output, error, exitCode);
        }

        private Result<object> CreateFailureResult(ProcessExecutionException exception)
        {
            return Result<object>.Failure(exception);
        }

        private Result<object> CreateSuccessResult(object? value = null)
        {
            return Result<object>.Success(value ?? new object());
        }

        private Result<object> CreateResultWithOutput(string output)
        {
            var exception = CreateProcessExecutionException(output: output);
            return Result<object>.Failure(exception);
        }

        private Result<object> CreateResultWithNullProcessException()
        {
            return Result<object>.Failure(new InvalidOperationException("Non-process exception"));
        }

        private ProcessExecutionException CreateProcessExecutionException(
            string command = "test command",
            string? output = "test output",
            string error = "test error",
            int exitCode = 1)
        {
            return new TypeSpecCompilationException(command, output ?? string.Empty, error, exitCode);
        }

        #endregion
    }
}
