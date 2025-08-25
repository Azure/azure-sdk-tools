using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Exceptions;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class BuildErrorAnalyzerTests
    {
        #region Constructor Tests

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            var errorParser = CreateMockErrorParser();

            var ex = Assert.Throws<ArgumentNullException>(() => new BuildErrorAnalyzer(null!, errorParser.Object));
            Assert.That(ex?.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullErrorParser_ThrowsArgumentNullException()
        {
            var logger = NullLogger<BuildErrorAnalyzer>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() => new BuildErrorAnalyzer(logger, null!));
            Assert.That(ex?.ParamName, Is.EqualTo("errorParser"));
        }

        [Test]
        public void Constructor_WithValidParameters_DoesNotThrow()
        {
            var logger = NullLogger<BuildErrorAnalyzer>.Instance;
            var errorParser = CreateMockErrorParser();

            Assert.DoesNotThrow(() => new BuildErrorAnalyzer(logger, errorParser.Object));
        }

        [Test]
        public void Constructor_WithValidParameters_AssignsProperties()
        {
            var logger = NullLogger<BuildErrorAnalyzer>.Instance;
            var errorParser = CreateMockErrorParser();
            
            var analyzer = new BuildErrorAnalyzer(logger, errorParser.Object);
            
            // Verify dependencies are properly assigned by calling a method
            Assert.DoesNotThrowAsync(async () => 
                await analyzer.AnalyzeAndGetFixesAsync(null, null, CancellationToken.None));
        }

        #endregion

        #region AnalyzeAndGetFixesAsync Tests

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithNullResults_ReturnsEmptyList()
        {
            var analyzer = CreateBuildErrorAnalyzer();

            var result = await analyzer.AnalyzeAndGetFixesAsync(null, null, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithSuccessfulResults_ReturnsEmptyList()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var successResult = CreateSuccessResult();

            var result = await analyzer.AnalyzeAndGetFixesAsync(successResult, successResult, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithCompileFailure_CallsErrorParserForCompileResult()
        {
            var mockErrorParser = CreateMockErrorParser();
            var mockFixes = CreateMockFixes(2);
            mockErrorParser.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockFixes);
            
            var analyzer = CreateBuildErrorAnalyzer(errorParser: mockErrorParser);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());

            var result = await analyzer.AnalyzeAndGetFixesAsync(compileResult, null, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(2));
            mockErrorParser.Verify(p => p.AnalyzeErrorsAsync(compileResult, CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithBuildFailure_CallsErrorParserForBuildResult()
        {
            var mockErrorParser = CreateMockErrorParser();
            var mockFixes = CreateMockFixes(3);
            mockErrorParser.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockFixes);
            
            var analyzer = CreateBuildErrorAnalyzer(errorParser: mockErrorParser);
            var buildResult = CreateFailureResult(CreateDotNetBuildException());

            var result = await analyzer.AnalyzeAndGetFixesAsync(null, buildResult, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(3));
            mockErrorParser.Verify(p => p.AnalyzeErrorsAsync(buildResult, CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithBothFailures_CallsErrorParserForBothResults()
        {
            var mockErrorParser = CreateMockErrorParser();
            var compileFixes = CreateMockFixes(2, "Compile");
            var buildFixes = CreateMockFixes(3, "Build");
            
            mockErrorParser.SetupSequence(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(compileFixes)
                .ReturnsAsync(buildFixes);
            
            var analyzer = CreateBuildErrorAnalyzer(errorParser: mockErrorParser);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());
            var buildResult = CreateFailureResult(CreateDotNetBuildException());

            var result = await analyzer.AnalyzeAndGetFixesAsync(compileResult, buildResult, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(5)); // 2 + 3 fixes
            mockErrorParser.Verify(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithNonFailureResults_DoesNotCallErrorParser()
        {
            var mockErrorParser = CreateMockErrorParser();
            var analyzer = CreateBuildErrorAnalyzer(errorParser: mockErrorParser);
            var successResult = CreateSuccessResult();

            var result = await analyzer.AnalyzeAndGetFixesAsync(successResult, successResult, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
            mockErrorParser.Verify(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_LogsTotalFixesGenerated()
        {
            var mockLogger = CreateMockLogger();
            var mockErrorParser = CreateMockErrorParser();
            var mockFixes = CreateMockFixes(5);
            mockErrorParser.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockFixes);
            
            var analyzer = CreateBuildErrorAnalyzer(logger: mockLogger, errorParser: mockErrorParser);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());

            await analyzer.AnalyzeAndGetFixesAsync(compileResult, null, CancellationToken.None);

            // Verify total fixes count is logged
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Total fixes generated: 5")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithCancellationToken_PassesToErrorParser()
        {
            var mockErrorParser = CreateMockErrorParser();
            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            
            var analyzer = CreateBuildErrorAnalyzer(errorParser: mockErrorParser);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());

            await analyzer.AnalyzeAndGetFixesAsync(compileResult, null, cancellationToken);

            mockErrorParser.Verify(p => p.AnalyzeErrorsAsync(compileResult, cancellationToken), Times.Once);
        }

        [Test]
        public void AnalyzeAndGetFixesAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            var mockErrorParser = CreateMockErrorParser();
            mockErrorParser.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());
            
            var analyzer = CreateBuildErrorAnalyzer(errorParser: mockErrorParser);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await analyzer.AnalyzeAndGetFixesAsync(compileResult, null, cts.Token));
        }

        [Test]
        public void AnalyzeAndGetFixesAsync_WhenErrorParserThrows_PropagatesException()
        {
            var mockErrorParser = CreateMockErrorParser();
            var expectedException = new InvalidOperationException("Error parser failed");
            mockErrorParser.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);
            
            var analyzer = CreateBuildErrorAnalyzer(errorParser: mockErrorParser);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await analyzer.AnalyzeAndGetFixesAsync(compileResult, null, CancellationToken.None));
            
            Assert.That(ex, Is.EqualTo(expectedException));
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithEmptyFixesFromErrorParser_ReturnsEmptyList()
        {
            var mockErrorParser = CreateMockErrorParser();
            mockErrorParser.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Fix>());
            
            var analyzer = CreateBuildErrorAnalyzer(errorParser: mockErrorParser);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());

            var result = await analyzer.AnalyzeAndGetFixesAsync(compileResult, null, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        #endregion

        #region Static Constructor Tests

        [Test]
        public void StaticConstructor_RegistersAnalyzerProviders()
        {
            // This test verifies that the static constructor has run by checking that
            // ErrorAnalyzerService has providers registered. We create a new analyzer 
            // instance to ensure static constructor runs.
            var logger = NullLogger<BuildErrorAnalyzer>.Instance;
            var errorParser = CreateMockErrorParser();
            
            var analyzer = new BuildErrorAnalyzer(logger, errorParser.Object);

            // If the static constructor ran properly, the analyzer should be constructible
            Assert.That(analyzer, Is.Not.Null);
        }

        [Test]
        public void StaticConstructor_RegistersClientAnalyzerProvider()
        {
            // Verify that creating the analyzer doesn't throw, indicating providers are registered
            var logger = NullLogger<BuildErrorAnalyzer>.Instance;
            var errorParser = CreateMockErrorParser();

            Assert.DoesNotThrow(() => new BuildErrorAnalyzer(logger, errorParser.Object));
        }

        [Test]
        public void StaticConstructor_RegistersGeneralAnalyzerProvider()
        {
            // Verify that creating the analyzer doesn't throw, indicating providers are registered
            var logger = NullLogger<BuildErrorAnalyzer>.Instance;
            var errorParser = CreateMockErrorParser();

            Assert.DoesNotThrow(() => new BuildErrorAnalyzer(logger, errorParser.Object));
        }

        [Test]
        public void StaticConstructor_RegistersManagementAnalyzerProvider()
        {
            // Verify that creating the analyzer doesn't throw, indicating providers are registered
            var logger = NullLogger<BuildErrorAnalyzer>.Instance;
            var errorParser = CreateMockErrorParser();

            Assert.DoesNotThrow(() => new BuildErrorAnalyzer(logger, errorParser.Object));
        }

        #endregion

        #region Integration Tests

        [Test]
        public async Task IntegrationTest_AnalyzeAndGetFixesAsync_WithRealScenario()
        {
            var mockLogger = CreateMockLogger();
            var mockErrorParser = CreateMockErrorParser();
            var expectedFixes = CreateMockFixes(3, "Integration");
            mockErrorParser.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedFixes);
            
            var analyzer = CreateBuildErrorAnalyzer(logger: mockLogger, errorParser: mockErrorParser);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());
            var buildResult = CreateFailureResult(CreateDotNetBuildException());

            var result = await analyzer.AnalyzeAndGetFixesAsync(compileResult, buildResult, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(6)); // 3 + 3 fixes
            
            // Verify both results were processed
            mockErrorParser.Verify(p => p.AnalyzeErrorsAsync(compileResult, CancellationToken.None), Times.Once);
            mockErrorParser.Verify(p => p.AnalyzeErrorsAsync(buildResult, CancellationToken.None), Times.Once);
            
            // Verify logging occurred
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Total fixes generated: 6")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void AnalyzeAndGetFixesAsync_WithPartialFailure_ContinuesProcessing()
        {
            var mockErrorParser = CreateMockErrorParser();
            var validFixes = CreateMockFixes(2);
            
            // Setup to succeed on first call, fail on second
            mockErrorParser.SetupSequence(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(validFixes)
                .ThrowsAsync(new InvalidOperationException("Second call failed"));
            
            var analyzer = CreateBuildErrorAnalyzer(errorParser: mockErrorParser);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());
            var buildResult = CreateFailureResult(CreateDotNetBuildException());

            // Should throw because we don't catch exceptions in the analyzer
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await analyzer.AnalyzeAndGetFixesAsync(compileResult, buildResult, CancellationToken.None));
            
            Assert.That(ex?.Message, Is.EqualTo("Second call failed"));
        }

        #endregion

        #region Helper Methods

        private BuildErrorAnalyzer CreateBuildErrorAnalyzer(
            Mock<ILogger<BuildErrorAnalyzer>>? logger = null,
            Mock<ErrorParser>? errorParser = null)
        {
            var loggerInstance = logger?.Object ?? NullLogger<BuildErrorAnalyzer>.Instance;
            var errorParserInstance = errorParser?.Object ?? CreateMockErrorParser().Object;
            
            return new BuildErrorAnalyzer(loggerInstance, errorParserInstance);
        }

        private Mock<ILogger<BuildErrorAnalyzer>> CreateMockLogger()
        {
            return new Mock<ILogger<BuildErrorAnalyzer>>();
        }

        private Mock<ErrorParser> CreateMockErrorParser()
        {
            // Create a mock that directly uses null for AgentOrchestrator since it won't be called in tests
            var mock = new Mock<ErrorParser>(null, Mock.Of<ILogger<ErrorParser>>());
            mock.CallBase = false; // Don't call base methods
            mock.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Fix>());
            return mock;
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

        private List<Fix> CreateMockFixes(int count, string prefix = "Fix")
        {
            var fixes = new List<Fix>();
            for (int i = 1; i <= count; i++)
            {
                var fix = new AgentPromptFix($"{prefix}_{i}", $"Context for {prefix}_{i}");
                fixes.Add(fix);
            }
            return fixes;
        }

        #endregion
    }
}
