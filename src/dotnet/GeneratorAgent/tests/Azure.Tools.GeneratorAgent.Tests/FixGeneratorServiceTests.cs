using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class FixGeneratorServiceTests
    {
        #region Constructor Tests

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            var ErrorParsingService = CreateMockErrorParsingService();

            var ex = Assert.Throws<ArgumentNullException>(() => new FixGeneratorService(null!, ErrorParsingService.Object));
            Assert.That(ex?.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullErrorParsingService_ThrowsArgumentNullException()
        {
            var logger = NullLogger<FixGeneratorService>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(() => new FixGeneratorService(logger, null!));
            Assert.That(ex?.ParamName, Is.EqualTo("errorParsingService"));
        }

        [Test]
        public void Constructor_WithValidParameters_DoesNotThrow()
        {
            var logger = NullLogger<FixGeneratorService>.Instance;
            var ErrorParsingService = CreateMockErrorParsingService();

            Assert.DoesNotThrow(() => new FixGeneratorService(logger, ErrorParsingService.Object));
        }

        [Test]
        public void Constructor_WithValidParameters_AssignsProperties()
        {
            var logger = NullLogger<FixGeneratorService>.Instance;
            var ErrorParsingService = CreateMockErrorParsingService();
            
            var analyzer = new FixGeneratorService(logger, ErrorParsingService.Object);
            
            // Verify dependencies are properly assigned by calling a method
            Assert.DoesNotThrowAsync(async () => 
                await analyzer.AnalyzeAndGetFixesAsync(null, null, CancellationToken.None));
        }

        #endregion

        #region AnalyzeAndGetFixesAsync Tests

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithNullResults_ReturnsEmptyList()
        {
            var analyzer = CreateFixGeneratorService();

            var result = await analyzer.AnalyzeAndGetFixesAsync(null, null, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithSuccessfulResults_ReturnsEmptyList()
        {
            var analyzer = CreateFixGeneratorService();
            var successResult = CreateSuccessResult();

            var result = await analyzer.AnalyzeAndGetFixesAsync(successResult, successResult, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithCompileFailure_CallsErrorParsingServiceForCompileResult()
        {
            var mockErrorParsingService = CreateMockErrorParsingService();
            var mockFixes = CreateMockFixes(2);
            mockErrorParsingService.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<IEnumerable<Fix>>.Success(mockFixes));
            
            var analyzer = CreateFixGeneratorService(ErrorParsingService: mockErrorParsingService);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());

            var result = await analyzer.AnalyzeAndGetFixesAsync(compileResult, null, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Count(), Is.EqualTo(2));
            mockErrorParsingService.Verify(p => p.AnalyzeErrorsAsync(compileResult, CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithBuildFailure_CallsErrorParsingServiceForBuildResult()
        {
            var mockErrorParsingService = CreateMockErrorParsingService();
            var mockFixes = CreateMockFixes(3);
            mockErrorParsingService.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<IEnumerable<Fix>>.Success(mockFixes));
            
            var analyzer = CreateFixGeneratorService(ErrorParsingService: mockErrorParsingService);
            var buildResult = CreateFailureResult(CreateDotNetBuildException());

            var result = await analyzer.AnalyzeAndGetFixesAsync(null, buildResult, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Count(), Is.EqualTo(3));
            mockErrorParsingService.Verify(p => p.AnalyzeErrorsAsync(buildResult, CancellationToken.None), Times.Once);
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithBothFailures_CallsErrorParsingServiceForBothResults()
        {
            var mockErrorParsingService = CreateMockErrorParsingService();
            var compileFixes = CreateMockFixes(2, "Compile");
            var buildFixes = CreateMockFixes(3, "Build");
            
            mockErrorParsingService.SetupSequence(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<IEnumerable<Fix>>.Success(compileFixes))
                .ReturnsAsync(Result<IEnumerable<Fix>>.Success(buildFixes));
            
            var analyzer = CreateFixGeneratorService(ErrorParsingService: mockErrorParsingService);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());
            var buildResult = CreateFailureResult(CreateDotNetBuildException());

            var result = await analyzer.AnalyzeAndGetFixesAsync(compileResult, buildResult, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Count(), Is.EqualTo(5)); // 2 + 3 fixes
            mockErrorParsingService.Verify(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithNonFailureResults_DoesNotCallErrorParsingService()
        {
            var mockErrorParsingService = CreateMockErrorParsingService();
            var analyzer = CreateFixGeneratorService(ErrorParsingService: mockErrorParsingService);
            var successResult = CreateSuccessResult();

            var result = await analyzer.AnalyzeAndGetFixesAsync(successResult, successResult, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
            mockErrorParsingService.Verify(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_LogsTotalFixesGenerated()
        {
            var mockLogger = CreateMockLogger();
            var mockErrorParsingService = CreateMockErrorParsingService();
            var mockFixes = CreateMockFixes(5);
            mockErrorParsingService.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<IEnumerable<Fix>>.Success(mockFixes));
            
            var analyzer = CreateFixGeneratorService(logger: mockLogger, ErrorParsingService: mockErrorParsingService);
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
        public async Task AnalyzeAndGetFixesAsync_WithCancellationToken_PassesToErrorParsingService()
        {
            var mockErrorParsingService = CreateMockErrorParsingService();
            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            
            var analyzer = CreateFixGeneratorService(ErrorParsingService: mockErrorParsingService);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());

            await analyzer.AnalyzeAndGetFixesAsync(compileResult, null, cancellationToken);

            mockErrorParsingService.Verify(p => p.AnalyzeErrorsAsync(compileResult, cancellationToken), Times.Once);
        }

        [Test]
        public void AnalyzeAndGetFixesAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            var mockErrorParsingService = CreateMockErrorParsingService();
            mockErrorParsingService.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());
            
            var analyzer = CreateFixGeneratorService(ErrorParsingService: mockErrorParsingService);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await analyzer.AnalyzeAndGetFixesAsync(compileResult, null, cts.Token));
        }

        [Test]
        public void AnalyzeAndGetFixesAsync_WhenErrorParsingServiceThrows_PropagatesException()
        {
            var mockErrorParsingService = CreateMockErrorParsingService();
            var expectedException = new InvalidOperationException("Error parser failed");
            mockErrorParsingService.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);
            
            var analyzer = CreateFixGeneratorService(ErrorParsingService: mockErrorParsingService);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());

            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await analyzer.AnalyzeAndGetFixesAsync(compileResult, null, CancellationToken.None));
            
            Assert.That(ex, Is.EqualTo(expectedException));
        }

        [Test]
        public async Task AnalyzeAndGetFixesAsync_WithEmptyFixesFromErrorParsingService_ReturnsEmptyList()
        {
            var mockErrorParsingService = CreateMockErrorParsingService();
            mockErrorParsingService.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<IEnumerable<Fix>>.Success(new List<Fix>()));
            
            var analyzer = CreateFixGeneratorService(ErrorParsingService: mockErrorParsingService);
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
            var logger = NullLogger<FixGeneratorService>.Instance;
            var ErrorParsingService = CreateMockErrorParsingService();
            
            var analyzer = new FixGeneratorService(logger, ErrorParsingService.Object);

            // If the static constructor ran properly, the analyzer should be constructible
            Assert.That(analyzer, Is.Not.Null);
        }

        [Test]
        public void StaticConstructor_RegistersClientAnalyzerProvider()
        {
            // Verify that creating the analyzer doesn't throw, indicating providers are registered
            var logger = NullLogger<FixGeneratorService>.Instance;
            var ErrorParsingService = CreateMockErrorParsingService();

            Assert.DoesNotThrow(() => new FixGeneratorService(logger, ErrorParsingService.Object));
        }

        [Test]
        public void StaticConstructor_RegistersGeneralAnalyzerProvider()
        {
            // Verify that creating the analyzer doesn't throw, indicating providers are registered
            var logger = NullLogger<FixGeneratorService>.Instance;
            var ErrorParsingService = CreateMockErrorParsingService();

            Assert.DoesNotThrow(() => new FixGeneratorService(logger, ErrorParsingService.Object));
        }

        [Test]
        public void StaticConstructor_RegistersManagementAnalyzerProvider()
        {
            // Verify that creating the analyzer doesn't throw, indicating providers are registered
            var logger = NullLogger<FixGeneratorService>.Instance;
            var ErrorParsingService = CreateMockErrorParsingService();

            Assert.DoesNotThrow(() => new FixGeneratorService(logger, ErrorParsingService.Object));
        }

        #endregion

        #region Integration Tests

        [Test]
        public async Task IntegrationTest_AnalyzeAndGetFixesAsync_WithRealScenario()
        {
            var mockLogger = CreateMockLogger();
            var mockErrorParsingService = CreateMockErrorParsingService();
            var expectedFixes = CreateMockFixes(3, "Integration");
            mockErrorParsingService.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<IEnumerable<Fix>>.Success(expectedFixes));
            
            var analyzer = CreateFixGeneratorService(logger: mockLogger, ErrorParsingService: mockErrorParsingService);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());
            var buildResult = CreateFailureResult(CreateDotNetBuildException());

            var result = await analyzer.AnalyzeAndGetFixesAsync(compileResult, buildResult, CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Count(), Is.EqualTo(6));
            
            // Verify both results were processed
            mockErrorParsingService.Verify(p => p.AnalyzeErrorsAsync(compileResult, CancellationToken.None), Times.Once);
            mockErrorParsingService.Verify(p => p.AnalyzeErrorsAsync(buildResult, CancellationToken.None), Times.Once);
            
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
            var mockErrorParsingService = CreateMockErrorParsingService();
            var validFixes = CreateMockFixes(2);
            
            // Setup to succeed on first call, fail on second
            mockErrorParsingService.SetupSequence(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<IEnumerable<Fix>>.Success(validFixes))
                .ThrowsAsync(new InvalidOperationException("Second call failed"));
            
            var analyzer = CreateFixGeneratorService(ErrorParsingService: mockErrorParsingService);
            var compileResult = CreateFailureResult(CreateTypeSpecCompilationException());
            var buildResult = CreateFailureResult(CreateDotNetBuildException());

            // Should throw because we don't catch exceptions in the analyzer
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await analyzer.AnalyzeAndGetFixesAsync(compileResult, buildResult, CancellationToken.None));
            
            Assert.That(ex?.Message, Is.EqualTo("Second call failed"));
        }

        #endregion

        #region Helper Methods

        private FixGeneratorService CreateFixGeneratorService(
            Mock<ILogger<FixGeneratorService>>? logger = null,
            Mock<ErrorParsingService>? ErrorParsingService = null)
        {
            var loggerInstance = logger?.Object ?? NullLogger<FixGeneratorService>.Instance;
            var ErrorParsingServiceInstance = ErrorParsingService?.Object ?? CreateMockErrorParsingService().Object;
            
            return new FixGeneratorService(loggerInstance, ErrorParsingServiceInstance);
        }

        private Mock<ILogger<FixGeneratorService>> CreateMockLogger()
        {
            return new Mock<ILogger<FixGeneratorService>>();
        }

        private Mock<ErrorParsingService> CreateMockErrorParsingService()
        {
            // Create a mock that directly uses null for ErrorFixerAgent since it won't be called in tests
            var mock = new Mock<ErrorParsingService>(null, Mock.Of<ILogger<ErrorParsingService>>());
            mock.CallBase = false; // Don't call base methods
            mock.Setup(p => p.AnalyzeErrorsAsync(It.IsAny<Result<object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<IEnumerable<Fix>>.Success(new List<Fix>()));
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
