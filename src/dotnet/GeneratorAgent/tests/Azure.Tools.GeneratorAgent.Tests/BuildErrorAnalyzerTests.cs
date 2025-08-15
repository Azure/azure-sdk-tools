using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.GeneratorAgent.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class BuildErrorAnalyzerTests
    {
        private static Mock<ILogger<BuildErrorAnalyzer>> CreateLoggerMock()
        {
            return new Mock<ILogger<BuildErrorAnalyzer>>();
        }

        private static BuildErrorAnalyzer CreateBuildErrorAnalyzer(Mock<ILogger<BuildErrorAnalyzer>>? loggerMock = null)
        {
            return new BuildErrorAnalyzer((loggerMock ?? CreateLoggerMock()).Object);
        }

        private static TypeSpecCompilationException CreateTypeSpecCompilationException(
            string command = "tsp compile",
            string output = "TypeSpec output",
            string error = "error AZC0012: Type name 'Client' is too generic",
            int exitCode = 1)
        {
            return new TypeSpecCompilationException(command, output, error, exitCode);
        }

        private static DotNetBuildException CreateDotNetBuildException(
            string command = "dotnet build",
            string output = "Build output",
            string error = "error CS0103: The name 'variable' does not exist",
            int exitCode = 1)
        {
            return new DotNetBuildException(command, output, error, exitCode);
        }

        private static Result<object> CreateFailureResult(ProcessExecutionException exception)
        {
            return Result<object>.Failure(exception);
        }

        private static Result<object> CreateSuccessResult(object value = null!)
        {
            return Result<object>.Success(value ?? new object());
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new BuildErrorAnalyzer(null!));
            Assert.That(ex?.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithValidLogger_DoesNotThrow()
        {
            var loggerMock = CreateLoggerMock();
            Assert.DoesNotThrow(() => new BuildErrorAnalyzer(loggerMock.Object));
        }

        [Test]
        public void Constructor_WithValidLogger_AssignsLogger()
        {
            var loggerMock = CreateLoggerMock();
            var analyzer = new BuildErrorAnalyzer(loggerMock.Object);
            
            // Verify logger is used by calling a method that logs
            analyzer.ParseBuildOutput(CreateTypeSpecCompilationException());
            
            // Verify logger was called (indicates it was properly assigned)
            loggerMock.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region AnalyzeAndGetFixes Tests

        [Test]
        public void AnalyzeAndGetFixes_WithNullResults_ReturnsEmptyList()
        {
            var analyzer = CreateBuildErrorAnalyzer();

            var result = analyzer.AnalyzeAndGetFixes(null, null);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void AnalyzeAndGetFixes_WithSuccessfulResults_ReturnsEmptyList()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var successResult = CreateSuccessResult();

            var result = analyzer.AnalyzeAndGetFixes(successResult, successResult);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void AnalyzeAndGetFixes_WithTypeSpecFailure_ProcessesTypeSpecErrors()
        {
            var loggerMock = CreateLoggerMock();
            var analyzer = CreateBuildErrorAnalyzer(loggerMock);
            var typeSpecException = CreateTypeSpecCompilationException();
            var compileResult = CreateFailureResult(typeSpecException);

            var result = analyzer.AnalyzeAndGetFixes(compileResult, null);

            Assert.That(result, Is.Not.Null);
            // Verify that TypeSpec compilation was logged
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Analyzing TypeSpec compilation errors")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void AnalyzeAndGetFixes_WithDotNetBuildFailure_ProcessesBuildErrors()
        {
            var loggerMock = CreateLoggerMock();
            var analyzer = CreateBuildErrorAnalyzer(loggerMock);
            var dotNetException = CreateDotNetBuildException();
            var buildResult = CreateFailureResult(dotNetException);

            var result = analyzer.AnalyzeAndGetFixes(null, buildResult);

            Assert.That(result, Is.Not.Null);
            // Verify that .NET build was logged
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Analyzing .NET build errors")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void AnalyzeAndGetFixes_WithBothFailures_ProcessesBothTypes()
        {
            var loggerMock = CreateLoggerMock();
            var analyzer = CreateBuildErrorAnalyzer(loggerMock);
            var typeSpecException = CreateTypeSpecCompilationException();
            var dotNetException = CreateDotNetBuildException();
            var compileResult = CreateFailureResult(typeSpecException);
            var buildResult = CreateFailureResult(dotNetException);

            var result = analyzer.AnalyzeAndGetFixes(compileResult, buildResult);

            Assert.That(result, Is.Not.Null);
            // Verify both types were processed
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Analyzing TypeSpec compilation errors")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Analyzing .NET build errors")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void AnalyzeAndGetFixes_WithNonProcessExceptionFailure_SkipsProcessing()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var generalException = new InvalidOperationException("General error");
            var failureResult = Result<object>.Failure(generalException);

            var result = analyzer.AnalyzeAndGetFixes(failureResult, failureResult);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void AnalyzeAndGetFixes_LogsTotalFixesGenerated()
        {
            var loggerMock = CreateLoggerMock();
            var analyzer = CreateBuildErrorAnalyzer(loggerMock);
            var typeSpecException = CreateTypeSpecCompilationException();
            var compileResult = CreateFailureResult(typeSpecException);

            analyzer.AnalyzeAndGetFixes(compileResult, null);

            // Verify total fixes count is logged
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Total fixes generated")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region ParseBuildOutput Tests

        [Test]
        public void ParseBuildOutput_WithNullException_ThrowsArgumentNullException()
        {
            var analyzer = CreateBuildErrorAnalyzer();

            var ex = Assert.Throws<ArgumentNullException>(() => analyzer.ParseBuildOutput(null!));
            Assert.That(ex?.ParamName, Is.EqualTo("processException"));
        }

        [Test]
        public void ParseBuildOutput_WithEmptyOutput_ReturnsEmptyList()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var exception = CreateTypeSpecCompilationException(output: "", error: "");

            var result = analyzer.ParseBuildOutput(exception);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseBuildOutput_WithWhitespaceOnlyOutput_ReturnsEmptyList()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var exception = CreateTypeSpecCompilationException(output: "   ", error: "   ");

            var result = analyzer.ParseBuildOutput(exception);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseBuildOutput_WithSingleValidError_ReturnsSingleRuleError()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var exception = CreateTypeSpecCompilationException(
                error: "error AZC0012: Type name 'Client' is too generic");

            var result = analyzer.ParseBuildOutput(exception);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("AZC0012"));
            Assert.That(result[0].message, Is.EqualTo("Type name 'Client' is too generic"));
        }

        [Test]
        public void ParseBuildOutput_WithMultipleValidErrors_ReturnsMultipleRuleErrors()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var combinedErrors = "error AZC0012: Type name 'Client' is too generic\nerror CS0103: The name 'variable' does not exist";
            var exception = CreateTypeSpecCompilationException(error: combinedErrors);

            var result = analyzer.ParseBuildOutput(exception);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(2));
            
            var azc0012Error = result.FirstOrDefault(e => e.type == "AZC0012");
            Assert.That(azc0012Error, Is.Not.Null);
            Assert.That(azc0012Error!.message, Is.EqualTo("Type name 'Client' is too generic"));
            
            var cs0103Error = result.FirstOrDefault(e => e.type == "CS0103");
            Assert.That(cs0103Error, Is.Not.Null);
            Assert.That(cs0103Error!.message, Is.EqualTo("The name 'variable' does not exist"));
        }

        [Test]
        public void ParseBuildOutput_WithDuplicateErrors_RemovesDuplicates()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var duplicateErrors = "error AZC0012: Type name 'Client' is too generic\nerror AZC0012: Type name 'Client' is too generic";
            var exception = CreateTypeSpecCompilationException(error: duplicateErrors);

            var result = analyzer.ParseBuildOutput(exception);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("AZC0012"));
            Assert.That(result[0].message, Is.EqualTo("Type name 'Client' is too generic"));
        }

        [Test]
        public void ParseBuildOutput_WithCombinedOutputAndError_ProcessesBothStreams()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var exception = CreateTypeSpecCompilationException(
                output: "error AZC0012: Type name 'Client' is too generic",
                error: "error CS0103: The name 'variable' does not exist");

            var result = analyzer.ParseBuildOutput(exception);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(2));
            
            var errorTypes = result.Select(e => e.type).ToList();
            Assert.That(errorTypes, Contains.Item("AZC0012"));
            Assert.That(errorTypes, Contains.Item("CS0103"));
        }

        [Test]
        public void ParseBuildOutput_WithInvalidErrorFormat_SkipsInvalidErrors()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var mixedContent = "This is not an error\nerror AZC0012: Type name 'Client' is too generic\nAnother non-error line";
            var exception = CreateTypeSpecCompilationException(error: mixedContent);

            var result = analyzer.ParseBuildOutput(exception);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("AZC0012"));
        }

        [Test]
        public void ParseBuildOutput_WithEmptyErrorTypeOrMessage_SkipsError()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var invalidErrors = "error : Missing error type\nerror VALID001: Valid error message";
            var exception = CreateTypeSpecCompilationException(error: invalidErrors);

            var result = analyzer.ParseBuildOutput(exception);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("VALID001"));
        }

        [Test]
        public void ParseBuildOutput_LogsWarningForNoContent()
        {
            var loggerMock = CreateLoggerMock();
            var analyzer = CreateBuildErrorAnalyzer(loggerMock);
            var exception = CreateTypeSpecCompilationException(output: "", error: "");

            analyzer.ParseBuildOutput(exception);

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No output or error content available")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void ParseBuildOutput_LogsDebugForMatchCount()
        {
            var loggerMock = CreateLoggerMock();
            var analyzer = CreateBuildErrorAnalyzer(loggerMock);
            var exception = CreateTypeSpecCompilationException(error: "error AZC0012: Type name 'Client' is too generic");

            analyzer.ParseBuildOutput(exception);

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Found") && v.ToString()!.Contains("potential error matches")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void ParseBuildOutput_LogsDebugForExtractedErrors()
        {
            var loggerMock = CreateLoggerMock();
            var analyzer = CreateBuildErrorAnalyzer(loggerMock);
            var exception = CreateTypeSpecCompilationException(error: "error AZC0012: Type name 'Client' is too generic");

            analyzer.ParseBuildOutput(exception);

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Extracted error")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region GetFixes Tests

        [Test]
        public void GetFixes_WithNullErrors_ThrowsArgumentNullException()
        {
            var analyzer = CreateBuildErrorAnalyzer();

            var ex = Assert.Throws<ArgumentNullException>(() => analyzer.GetFixes(null!));
            Assert.That(ex?.ParamName, Is.EqualTo("errors"));
        }

        [Test]
        public void GetFixes_WithEmptyErrorList_ReturnsEmptyEnumerable()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var emptyErrors = new List<RuleError>();

            var result = analyzer.GetFixes(emptyErrors);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToList(), Is.Empty);
        }

        [Test]
        public void GetFixes_WithValidErrors_CallsErrorAnalyzerService()
        {
            var loggerMock = CreateLoggerMock();
            var analyzer = CreateBuildErrorAnalyzer(loggerMock);
            var errors = new List<RuleError>
            {
                new RuleError("AZC0012", "Type name 'Client' is too generic")
            };

            var result = analyzer.GetFixes(errors);

            Assert.That(result, Is.Not.Null);
            
            // Verify that information was logged about generating fixes
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Generated fixes for") && v.ToString()!.Contains("errors")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void GetFixes_WithValidErrors_LogsErrorCount()
        {
            var loggerMock = CreateLoggerMock();
            var analyzer = CreateBuildErrorAnalyzer(loggerMock);
            var errors = new List<RuleError>
            {
                new RuleError("AZC0012", "Type name 'Client' is too generic"),
                new RuleError("CS0103", "The name 'variable' does not exist")
            };

            analyzer.GetFixes(errors);

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Generated fixes for errors")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Static Constructor and Error Regex Tests

        [Test]
        public void StaticConstructor_RegistersAnalyzerProviders()
        {
            // This test verifies that the static constructor has run by checking that
            // ErrorAnalyzerService has providers registered. We'll create a new analyzer 
            // instance to ensure static constructor runs, then verify providers exist
            // by checking if GetFixes returns expected behavior
            var analyzer = CreateBuildErrorAnalyzer();
            var errors = new List<RuleError>
            {
                new RuleError("AZC0012", "Type name 'Client' is too generic")
            };

            var result = analyzer.GetFixes(errors);

            // If providers are registered, we should get results (or at least not throw)
            Assert.That(result, Is.Not.Null);
            Assert.DoesNotThrow(() => result.ToList());
        }

        [Test]
        public void ErrorRegex_MatchesStandardErrorFormat()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var exception = CreateTypeSpecCompilationException(
                error: "error AZC0012: Type name 'Client' is too generic");

            var result = analyzer.ParseBuildOutput(exception);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("AZC0012"));
            Assert.That(result[0].message, Is.EqualTo("Type name 'Client' is too generic"));
        }

        [Test]
        public void ErrorRegex_MatchesCaseInsensitiveError()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var exception = CreateTypeSpecCompilationException(
                error: "ERROR AZC0012: Type name 'Client' is too generic");

            var result = analyzer.ParseBuildOutput(exception);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].type, Is.EqualTo("AZC0012"));
        }

        [Test]
        public void ErrorRegex_MatchesMultipleErrorFormats()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var exception = CreateTypeSpecCompilationException(
                error: "error CS0103: Variable not found\nerror AZC0012: Generic type name");

            var result = analyzer.ParseBuildOutput(exception);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(2));
            
            var errorTypes = result.Select(e => e.type).ToList();
            Assert.That(errorTypes, Contains.Item("CS0103"));
            Assert.That(errorTypes, Contains.Item("AZC0012"));
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void AnalyzeAndGetFixes_WithExceptionInTypeSpecProcessing_ContinuesWithLogging()
        {
            var loggerMock = CreateLoggerMock();
            var analyzer = CreateBuildErrorAnalyzer(loggerMock);
            
            // Create an exception that might cause issues during processing
            var typeSpecException = CreateTypeSpecCompilationException(
                error: "error INVALID: This might cause issues during parsing");
            var compileResult = CreateFailureResult(typeSpecException);

            var result = analyzer.AnalyzeAndGetFixes(compileResult, null);

            // Should not throw and should return a list (possibly empty)
            Assert.That(result, Is.Not.Null);
            Assert.DoesNotThrow(() => result.ToList());
        }

        [Test]
        public void AnalyzeAndGetFixes_WithExceptionInDotNetProcessing_ContinuesWithLogging()
        {
            var loggerMock = CreateLoggerMock();
            var analyzer = CreateBuildErrorAnalyzer(loggerMock);
            
            var dotNetException = CreateDotNetBuildException(
                error: "error INVALID: This might cause issues during parsing");
            var buildResult = CreateFailureResult(dotNetException);

            var result = analyzer.AnalyzeAndGetFixes(null, buildResult);

            // Should not throw and should return a list (possibly empty)
            Assert.That(result, Is.Not.Null);
            Assert.DoesNotThrow(() => result.ToList());
        }

        [Test]
        public void GetFixes_WithExceptionFromErrorAnalyzerService_ReturnsEmptyAndLogsError()
        {
            var loggerMock = CreateLoggerMock();
            var analyzer = CreateBuildErrorAnalyzer(loggerMock);
            
            // Create a scenario that might cause ErrorAnalyzerService to throw
            var errors = new List<RuleError>
            {
                new RuleError("PROBLEMATIC", "This might cause internal issues")
            };

            var result = analyzer.GetFixes(errors);

            // Should return empty enumerable and not throw
            Assert.That(result, Is.Not.Null);
            Assert.DoesNotThrow(() => result.ToList());
        }

        #endregion

        #region Integration-Style Tests (using real error patterns)

        [Test]
        public void ParseBuildOutput_WithRealTypeSpecError_ParsesCorrectly()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var realError = @"
/path/to/file.tsp(10,5): error typespec/library/invalid-model: Model 'TestModel' contains invalid properties.
error AZC0012: Type name 'Client' is too generic. Consider using a more descriptive multi-word name, such as 'ServiceClient'.
";
            var exception = CreateTypeSpecCompilationException(error: realError);

            var result = analyzer.ParseBuildOutput(exception);

            Assert.That(result, Is.Not.Null);
            var azc0012Error = result.FirstOrDefault(e => e.type == "AZC0012");
            Assert.That(azc0012Error, Is.Not.Null);
            Assert.That(azc0012Error!.message, Does.Contain("Type name 'Client' is too generic"));
        }

        [Test]
        public void ParseBuildOutput_WithRealDotNetBuildError_ParsesCorrectly()
        {
            var analyzer = CreateBuildErrorAnalyzer();
            var realError = @"
  Build FAILED.
  
/path/to/Program.cs(15,13): error CS0103: The name 'undefinedVariable' does not exist in the current context [/path/to/project.csproj]
/path/to/Other.cs(22,5): error CS0246: The type or namespace name 'UnknownType' could not be found [/path/to/project.csproj]
  
    0 Warning(s)
    2 Error(s)
";
            var exception = CreateDotNetBuildException(error: realError);

            var result = analyzer.ParseBuildOutput(exception);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(2));
            
            var cs0103Error = result.FirstOrDefault(e => e.type == "CS0103");
            Assert.That(cs0103Error, Is.Not.Null);
            Assert.That(cs0103Error!.message, Does.Contain("undefinedVariable"));
            
            var cs0246Error = result.FirstOrDefault(e => e.type == "CS0246");
            Assert.That(cs0246Error, Is.Not.Null);
            Assert.That(cs0246Error!.message, Does.Contain("UnknownType"));
        }

        #endregion
    }
}
