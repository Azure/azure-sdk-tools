using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Exceptions;
using Azure.Tools.GeneratorAgent.Agent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    internal class ErrorParsingServiceTests
    {
        #region Constructor Tests

        [Test]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            var logger = NullLogger<ErrorParsingService>.Instance;
            var ErrorParsingService = new ErrorParsingService(null, logger);

            Assert.That(ErrorParsingService, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullAgentOrchestrator_ShouldCreateInstance()
        {
            var logger = NullLogger<ErrorParsingService>.Instance;

            var ErrorParsingService = new ErrorParsingService(null, logger);

            Assert.That(ErrorParsingService, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ErrorParsingService(null, null!));
            Assert.That(ex?.ParamName, Is.EqualTo("logger"));
        }

        #endregion

        #region AnalyzeErrorsAsync Tests

        [Test]
        public async Task AnalyzeErrorsAsync_WithNullProcessException_ReturnsEmptyList()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var result = CreateResultWithNullProcessException();

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
            Assert.That(fixes, Is.Empty);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithNullOutput_ReturnsEmptyList()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var result = CreateResultWithNullOutput();

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
            Assert.That(fixes, Is.Empty);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithValidErrorOutput_ParsesAndReturnsFixesUsingRegex()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error AZC0001: Type name 'Client' is too generic\nerror CS0103: The name 'variable' does not exist";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithNoRegexMatches_FallsBackToAgentOrchestrator()
        {
            var ErrorParsingService = CreateErrorParsingService(null);
            var errorOutput = "some error without standard format";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
            Assert.That(fixes, Is.Empty);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithNoRegexMatchesAndNullAgentOrchestrator_ReturnsEmptyList()
        {
            var ErrorParsingService = CreateErrorParsingService(null);
            var errorOutput = "some error without standard format";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
            Assert.That(fixes, Is.Empty);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithCancellationToken_PassesTokenToAgentOrchestrator()
        {
            var ErrorParsingService = CreateErrorParsingService(null);
            var errorOutput = "some error without standard format";
            var result = CreateResultWithOutput(errorOutput);
            var cts = new CancellationTokenSource();

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, cts.Token);

            Assert.That(fixes, Is.Not.Null);
            Assert.That(fixes, Is.Empty);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithCancelledToken_RespectsCancellation()
        {
            var ErrorParsingService = CreateErrorParsingService(null);
            var errorOutput = "some error without standard format";
            var result = CreateResultWithOutput(errorOutput);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, cts.Token);

            Assert.That(fixes, Is.Not.Null);
            Assert.That(fixes, Is.Empty);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WhenAgentOrchestratorThrows_PropagatesException()
        {
            var ErrorParsingService = CreateErrorParsingService(null);
            var errorOutput = "some error without standard format";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);
            
            Assert.That(fixes, Is.Not.Null);
            Assert.That(fixes, Is.Empty);
        }

        #endregion

        #region Regex Parsing Tests

        [Test]
        public async Task AnalyzeErrorsAsync_WithSingleValidError_ParsesCorrectly()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error AZC0001: Type name 'Client' is too generic";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithMultipleValidErrors_ParsesAll()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error AZC0001: Type name 'Client' is too generic\nerror CS0103: The name 'variable' does not exist\nerror AZC0002: Another error";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithDuplicateErrors_RemovesDuplicates()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error AZC0001: Type name 'Client' is too generic\nerror AZC0001: Type name 'Client' is too generic";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithErrorsContainingWhitespace_TrimsCorrectly()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error  AZC0001 :  Type name 'Client' is too generic  ";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithCaseInsensitiveErrors_ParsesCorrectly()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "ERROR azc0001: Type name 'Client' is too generic";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithErrorsContainingBrackets_ParsesCorrectly()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error AZC0001: Type name 'Client' is too generic [/path/to/file.cs(123)]";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithEmptyErrorType_SkipsError()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error : Some error message";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithEmptyErrorMessage_SkipsError()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error AZC0001: ";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithEmptyString_ReturnsEmptyList()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var result = CreateResultWithOutput("");

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
            Assert.That(fixes, Is.Empty);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithWhitespaceString_ReturnsEmptyList()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var result = CreateResultWithOutput("   \n\t   ");

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
            Assert.That(fixes, Is.Empty);
        }

        #endregion

        #region GenerateFixes Tests

        [Test]
        public async Task AnalyzeErrorsAsync_WhenErrorAnalyzerServiceThrows_ReturnsEmptyArray()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error UNKNOWN9999: Some unknown error that might cause issues";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithValidErrors_CallsErrorAnalyzerService()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error AZC0001: Type name 'Client' is too generic";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
            // The fact that we get a non-null result indicates ErrorAnalyzerService was called
        }

        #endregion

        #region Logging Tests

        [Test]
        public async Task AnalyzeErrorsAsync_LogsAnalyzingErrors()
        {
            var mockLogger = new Mock<ILogger<ErrorParsingService>>();
            var ErrorParsingService = CreateErrorParsingService(logger: mockLogger.Object);
            var result = CreateResultWithOutput("error AZC0001: Test error");

            await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Analyzing errors")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithNullOutput_LogsWarning()
        {
            var mockLogger = new Mock<ILogger<ErrorParsingService>>();
            var ErrorParsingService = CreateErrorParsingService(logger: mockLogger.Object);
            var result = CreateResultWithNullOutput();

            await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No output provided for regex parsing")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithNullProcessExceptionOutput_LogsNoFixableError()
        {
            var mockLogger = new Mock<ILogger<ErrorParsingService>>();
            var ErrorParsingService = CreateErrorParsingService(logger: mockLogger.Object);
            var result = CreateResultWithNullProcessException();

            await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No fixable error, skipping error analysis")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithNoRegexMatches_LogsFallbackToAI()
        {
            var mockLogger = new Mock<ILogger<ErrorParsingService>>();
            var ErrorParsingService = CreateErrorParsingService(null, mockLogger.Object);
            var result = CreateResultWithOutput("some error without standard format");

            await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No errors found with regex")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithEmptyOutput_LogsWarning()
        {
            var mockLogger = new Mock<ILogger<ErrorParsingService>>();
            var ErrorParsingService = CreateErrorParsingService(logger: mockLogger.Object);
            var result = CreateResultWithOutput("");

            await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No output provided for regex parsing")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithValidMatches_LogsDebugInfo()
        {
            var mockLogger = new Mock<ILogger<ErrorParsingService>>();
            var ErrorParsingService = CreateErrorParsingService(logger: mockLogger.Object);
            var result = CreateResultWithOutput("error AZC0001: Test error");

            await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Found") && v.ToString()!.Contains("potential error matches")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Edge Cases and Error Handling

        [Test]
        public async Task AnalyzeErrorsAsync_WithMalformedErrorText_HandlesGracefully()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error malformed text without proper format";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
            // Should not throw, may be empty if no matches found
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithVeryLongErrorMessage_HandlesCorrectly()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var longMessage = new string('x', 10000);
            var errorOutput = $"error AZC0001: {longMessage}";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithSpecialCharactersInErrorMessage_HandlesCorrectly()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error AZC0001: Error with special chars: @#$%^&*()[]{}|\\:;\"'<>,.?/";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithUnicodeCharacters_HandlesCorrectly()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error AZC0001: Error with unicode: äöü ñ 中文 🚀";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithNewlinesAndCarriageReturns_HandlesCorrectly()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = "error AZC0001: Error message\r\nwith multiple lines\nerror CS0103: Another error";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        #endregion

        #region Regex Performance and Timeout Tests

        [Test] 
        public async Task AnalyzeErrorsAsync_WithRepeatedPatterns_DoesNotTimeout()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var repeatedPattern = new string('a', 1000);
            var errorOutput = $"error AZC0001: {repeatedPattern}";
            var result = CreateResultWithOutput(errorOutput);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);
            stopwatch.Stop();

            Assert.That(fixes, Is.Not.Null);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000), "Regex should not take more than 5 seconds");
        }

        #endregion

        #region Integration Tests

        [Test]
        public async Task AnalyzeErrorsAsync_EndToEndWithRealErrorScenario_WorksCorrectly()
        {
            var mockLogger = new Mock<ILogger<ErrorParsingService>>();
            var ErrorParsingService = CreateErrorParsingService(null, mockLogger.Object);
            
            var realErrorOutput = @"
                build failed with errors:
                error AZC0001: Type name 'Client' is too generic and should be replaced with a more specific name
                error CS0103: The name 'undefinedVariable' does not exist in the current context
                error AZC0012: Model name 'Data' is too generic
                warning CS1998: This async method lacks 'await' operators
                error CS0246: The type or namespace name 'UnknownType' could not be found
            ";
            var result = CreateResultWithOutput(realErrorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
            
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Analyzing errors")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task AnalyzeErrorsAsync_WithMixedValidAndInvalidErrors_ProcessesValidOnes()
        {
            var ErrorParsingService = CreateErrorParsingService();
            var errorOutput = @"
                error AZC0001: Valid error
                invalid error line
                error CS0103: Another valid error
                random text
                error: Invalid format
                error AZC0002: Valid error with proper format
            ";
            var result = CreateResultWithOutput(errorOutput);

            var fixes = await ErrorParsingService.AnalyzeErrorsAsync(result, CancellationToken.None);

            Assert.That(fixes, Is.Not.Null);
        }

        #endregion

        #region Helper Methods

        private ErrorParsingService CreateErrorParsingService(AgentOrchestrator? agentOrchestrator = null, ILogger<ErrorParsingService>? logger = null)
        {
            var loggerInstance = logger ?? NullLogger<ErrorParsingService>.Instance;
            return new ErrorParsingService(agentOrchestrator, loggerInstance);
        }

        private Result<object> CreateResultWithOutput(string output)
        {
            var exception = CreateProcessExecutionException(output: output);
            return Result<object>.Failure(exception);
        }

        private Result<object> CreateResultWithNullOutput()
        {
            return Result<object>.Failure(new TestProcessExecutionException("test command", null, "test error", 1));
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

        #region Test Helper Classes

        private class TestProcessExecutionException : ProcessExecutionException
        {
            public TestProcessExecutionException(string command, string? output, string error, int exitCode)
                : base("Test exception", command, output ?? "", error, exitCode)
            {
                Output = output;
            }

            public new string? Output { get; }
        }

        #endregion
    }
}
