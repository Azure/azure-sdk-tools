using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class CommandLineConfigurationTests
    {
        #region Helper Classes

        private sealed class TestDirectoryFixture : IDisposable
        {
            private readonly string _baseDirectory;
            private readonly List<string> _createdDirectories = new();

            public TestDirectoryFixture()
            {
                _baseDirectory = Path.Combine(Path.GetTempPath(), $"CLIConfigTest_{Guid.NewGuid():N}");
                Directory.CreateDirectory(_baseDirectory);
                _createdDirectories.Add(_baseDirectory);
            }

            public string CreateDirectory(string baseName)
            {
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var directory = Path.Combine(Path.GetTempPath(), $"{baseName}_{uniqueId}");
                Directory.CreateDirectory(directory);
                _createdDirectories.Add(directory);
                return directory;
            }

            public string CreateSubDirectory(string name)
            {
                var path = Path.Combine(_baseDirectory, name);
                Directory.CreateDirectory(path);
                _createdDirectories.Add(path);
                return path;
            }

            public void Dispose()
            {
                foreach (var directory in _createdDirectories.AsEnumerable().Reverse())
                {
                    if (Directory.Exists(directory))
                    {
                        try
                        {
                            Directory.Delete(directory, true);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        private static Mock<ILogger<CommandLineConfiguration>> CreateMockLogger()
        {
            return new Mock<ILogger<CommandLineConfiguration>>();
        }

        private static CommandLineConfiguration CreateCommandLineConfiguration(Mock<ILogger<CommandLineConfiguration>>? mockLogger = null)
        {
            return new CommandLineConfiguration((mockLogger ?? CreateMockLogger()).Object);
        }

        private static void VerifyLogError(Mock<ILogger<CommandLineConfiguration>> mockLogger, string expectedMessage)
        {
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        private static void VerifyLogInformation(Mock<ILogger<CommandLineConfiguration>> mockLogger, string expectedMessage)
        {
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region Constructor Tests

        [Test]
        public void Constructor_WithValidLogger_ShouldCreateInstance()
        {
            var mockLogger = CreateMockLogger();

            var config = CreateCommandLineConfiguration(mockLogger);

            Assert.That(config, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new CommandLineConfiguration(null!));

            Assert.That(exception?.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithValidLogger_StoresLoggerCorrectly()
        {
            var loggerMock = CreateMockLogger();
            var config = new CommandLineConfiguration(loggerMock.Object);
            
            // Verify logger is stored by triggering a validation that logs
            config.ValidateInput(null, null, "test");
            
            // Should have logged error for null input
            VerifyLogError(loggerMock, "Input validation failed");
        }

        #endregion

        #region CreateRootCommand Tests

        [Test]
        public void CreateRootCommand_WithNullHandler_CreatesRootCommandSuccessfully()
        {
            var config = CreateCommandLineConfiguration();
            
            var rootCommand = config.CreateRootCommand(null!);
            
            Assert.That(rootCommand, Is.Not.Null);
            Assert.That(rootCommand.Description, Is.EqualTo("Azure SDK Generator Agent"));
        }

        [Test]
        public void CreateRootCommand_WithValidHandler_ShouldCreateRootCommandWithCorrectDescription()
        {
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            Task<int> MockHandler(string? a, string? b, string c) => Task.FromResult(0);

            var rootCommand = commandLineConfiguration.CreateRootCommand(MockHandler);

            Assert.That(rootCommand, Is.Not.Null);
            Assert.That(rootCommand.Description, Is.EqualTo("Azure SDK Generator Agent"));
        }

        [Test]
        public void CreateRootCommand_ShouldHaveCorrectNumberOfOptions()
        {
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            Task<int> MockHandler(string? a, string? b, string c) => Task.FromResult(0);

            var rootCommand = commandLineConfiguration.CreateRootCommand(MockHandler);

            Assert.That(rootCommand.Options.Count, Is.EqualTo(3));
        }

        [Test]
        public void CreateRootCommand_ShouldHaveTypespecPathOption()
        {
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            Task<int> MockHandler(string? a, string? b, string c) => Task.FromResult(0);

            var rootCommand = commandLineConfiguration.CreateRootCommand(MockHandler);

            var typespecPathOption = rootCommand.Options.FirstOrDefault(o => o.Name == "typespec-dir");
            Assert.That(typespecPathOption, Is.Not.Null);
            Assert.That(typespecPathOption?.Description, Is.EqualTo("Path to the local TypeSpec project directory or TypeSpec specification directory (e.g., specification/testservice/TestService)"));
            Assert.That(typespecPathOption?.IsRequired, Is.True);
            
            // Check aliases
            Assert.That(typespecPathOption!.HasAlias("--typespec-dir"), Is.True);
            Assert.That(typespecPathOption.HasAlias("-t"), Is.True);
        }

        [Test]
        public void CreateRootCommand_ShouldHaveCommitIdOption()
        {
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            Task<int> MockHandler(string? a, string? b, string c) => Task.FromResult(0);

            var rootCommand = commandLineConfiguration.CreateRootCommand(MockHandler);

            var commitIdOption = rootCommand.Options.FirstOrDefault(o => o.Name == "commit-id");
            Assert.That(commitIdOption, Is.Not.Null);
            Assert.That(commitIdOption?.Description, Is.EqualTo("GitHub commit ID to generate SDK from (optional, used with --typespec-dir for GitHub generation)"));
            Assert.That(commitIdOption?.IsRequired, Is.False);
            
            // Check aliases
            Assert.That(commitIdOption!.HasAlias("--commit-id"), Is.True);
            Assert.That(commitIdOption.HasAlias("-c"), Is.True);
        }

        [Test]
        public void CreateRootCommand_ShouldHaveSdkPathOption()
        {
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            Task<int> MockHandler(string? a, string? b, string c) => Task.FromResult(0);

            var rootCommand = commandLineConfiguration.CreateRootCommand(MockHandler);

            var sdkPathOption = rootCommand.Options.FirstOrDefault(o => o.Name == "output-dir");
            Assert.That(sdkPathOption, Is.Not.Null);
            Assert.That(sdkPathOption?.Description, Is.EqualTo("Output directory for generated SDK files"));
            Assert.That(sdkPathOption?.IsRequired, Is.True);
            
            // Check aliases
            Assert.That(sdkPathOption!.HasAlias("--output-dir"), Is.True);
            Assert.That(sdkPathOption.HasAlias("-o"), Is.True);
        }

        [Test]
        public void CreateRootCommand_ShouldSetHandlerCorrectly()
        {
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            Task<int> MockHandler(string? a, string? b, string c) => Task.FromResult(0);

            var rootCommand = commandLineConfiguration.CreateRootCommand(MockHandler);

            Assert.That(rootCommand.Handler, Is.Not.Null);
        }

        [Test]
        public void CreateRootCommand_MultipleCallsWithSameHandler_ReturnConsistentResults()
        {
            var config = CreateCommandLineConfiguration();
            Task<int> handler(string? a, string? b, string c) => Task.FromResult(0);

            var rootCommand1 = config.CreateRootCommand(handler);
            var rootCommand2 = config.CreateRootCommand(handler);

            // Should be different instances but with same structure
            Assert.That(rootCommand1, Is.Not.SameAs(rootCommand2));
            Assert.That(rootCommand1.Description, Is.EqualTo(rootCommand2.Description));
            Assert.That(rootCommand1.Options.Count, Is.EqualTo(rootCommand2.Options.Count));
        }

        #endregion

        #region ValidateInput Tests

        [Test]
        public void ValidateInput_WithValidTypespecPath_ShouldReturnSuccess()
        {
            using var fixture = new TestDirectoryFixture();
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            
            // Create temporary directories with valid TypeSpec files
            var typespecPath = fixture.CreateDirectory("ValidTypeSpec");
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");
            
            File.WriteAllText(Path.Combine(typespecPath, "test.tsp"), "// TypeSpec file");
            
            const string? commitId = null;

            var result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(0));
            VerifyLogInformation(mockLogger, "Input validation completed successfully");
        }

        [Test]
        public void ValidateInput_WithTypespecPathAndCommitId_ShouldReturnSuccess()
        {
            using var fixture = new TestDirectoryFixture();
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            
            // For GitHub paths (with commit ID), we don't need local validation
            const string typespecPath = "specification/testservice/TestService";
            const string commitId = "abc123";
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            var result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(0));
            VerifyLogInformation(mockLogger, "Input validation completed successfully");
        }

        [Test]
        public void ValidateInput_WithGitHubStylePathAndCommitId_ReturnsSuccessExitCode()
        {
            using var tempDir = new TestDirectoryFixture();
            var loggerMock = CreateMockLogger();
            var config = CreateCommandLineConfiguration(loggerMock);

            var outputDir = tempDir.CreateSubDirectory("output");

            var result = config.ValidateInput("specification/testservice/TestService", "abc123def456", outputDir);

            Assert.That(result, Is.EqualTo(0));
        }

        [Test]
        public void ValidateInput_WithNullTypespecPath_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            const string? typespecPath = null;
            const string? commitId = null;
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            var result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "TypeSpec path cannot be null or empty");
        }

        [Test]
        public void ValidateInput_WithEmptyTypespecPath_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            const string typespecPath = "";
            const string? commitId = null;
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            var result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "TypeSpec path cannot be null or empty");
        }

        [Test]
        public void ValidateInput_WithWhitespaceTypespecPath_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            const string typespecPath = "   ";
            const string? commitId = null;
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            var result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "TypeSpec path cannot be null or empty");
        }

        [Test]
        public void ValidateInput_WithInvalidCommitId_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            const string typespecPath = "specification/testservice/TestService";
            const string commitId = "invalid-commit-id-with-special-chars!";
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            var result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "Commit ID must be 6-40 hexadecimal characters");
        }

        [Test]
        public void ValidateInput_WithInvalidOutputDirectory_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            
            // Create temporary directory with valid TypeSpec files
            var typespecPath = fixture.CreateDirectory("ValidTypeSpec");
            File.WriteAllText(Path.Combine(typespecPath, "test.tsp"), "// TypeSpec file");
            
            const string? commitId = null;
            const string sdkOutputPath = "../../../malicious/path"; // Path that will resolve to non-existent parent

            var result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "Parent directory does not exist");
        }

        [Test]
        public void ValidateInput_WithDirectoryTraversalInTypeSpecPath_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            const string typespecPath = "../../../malicious/typespec";
            const string? commitId = null;
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            var result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "TypeSpec directory not found");
        }

        [Test]
        public void ValidateInput_WithNonExistentLocalTypeSpecDirectory_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            var mockLogger = CreateMockLogger();
            var commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            const string? commitId = null;
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            var result = commandLineConfiguration.ValidateInput(nonExistentPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "TypeSpec directory not found");
        }

        [Test]
        public void ValidateInput_WithExceptionFromValidationContext_CatchesAndLogsArgumentException()
        {
            var loggerMock = CreateMockLogger();
            var config = CreateCommandLineConfiguration(loggerMock);
            
            // Use inputs that would cause ArgumentException during validation
            var result = config.ValidateInput("", null, "invalid-path-that-causes-exception");

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(loggerMock, "Input validation failed");
        }

        [Test]
        public void ValidateInput_LogsSpecificErrorMessages()
        {
            var loggerMock = CreateMockLogger();
            var config = CreateCommandLineConfiguration(loggerMock);
            
            config.ValidateInput(null, null, "test");

            // Verify the error message format and structure
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.StartsWith("Input validation failed:")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Test]
        public void ValidateInput_ReturnsCorrectExitCodes()
        {
            var config = CreateCommandLineConfiguration();

            // Test success scenario
            using var tempDir = new TestDirectoryFixture();
            var typeSpecDir = tempDir.CreateSubDirectory("typespec");
            File.WriteAllText(Path.Combine(typeSpecDir, "main.tsp"), "// TypeSpec content");
            var outputDir = tempDir.CreateSubDirectory("output");

            var successResult = config.ValidateInput(typeSpecDir, null, outputDir);
            Assert.That(successResult, Is.EqualTo(0)); // ExitCodeSuccess

            // Test failure scenario
            var failureResult = config.ValidateInput(null, null, "invalid");
            Assert.That(failureResult, Is.EqualTo(1)); // ExitCodeFailure
        }

        #endregion

        #region Integration and Thread Safety Tests

        [Test]
        public void FullWorkflow_CreateCommandAndValidateInputs_WorksCorrectly()
        {
            using var tempDir = new TestDirectoryFixture();
            var loggerMock = CreateMockLogger();
            var config = CreateCommandLineConfiguration(loggerMock);

            // Step 1: Create root command
            Task<int> handler(string? typeSpec, string? commit, string output) => 
                Task.FromResult(config.ValidateInput(typeSpec, commit, output));

            var rootCommand = config.CreateRootCommand(handler);

            // Step 2: Verify command structure
            Assert.That(rootCommand, Is.Not.Null);
            Assert.That(rootCommand.Options.Count, Is.EqualTo(3));

            // Step 3: Test validation with the same configuration
            var typeSpecDir = tempDir.CreateSubDirectory("typespec");
            File.WriteAllText(Path.Combine(typeSpecDir, "main.tsp"), "// TypeSpec content");
            var outputDir = tempDir.CreateSubDirectory("output");

            var validationResult = config.ValidateInput(typeSpecDir, null, outputDir);
            Assert.That(validationResult, Is.EqualTo(0));
        }

        [Test]
        public void CreateRootCommand_CalledConcurrently_ReturnsSeparateInstances()
        {
            var config = CreateCommandLineConfiguration();
            Task<int> handler1(string? a, string? b, string c) => Task.FromResult(0);
            Task<int> handler2(string? a, string? b, string c) => Task.FromResult(1);

            var task1 = Task.Run(() => config.CreateRootCommand(handler1));
            var task2 = Task.Run(() => config.CreateRootCommand(handler2));

            Task.WaitAll(task1, task2);

            var command1 = task1.Result;
            var command2 = task2.Result;

            Assert.That(command1, Is.Not.SameAs(command2));
            Assert.That(command1.Description, Is.EqualTo(command2.Description));
        }

        [Test]
        public void ValidateInput_CalledConcurrently_ReturnsConsistentResults()
        {
            using var tempDir = new TestDirectoryFixture();
            var config = CreateCommandLineConfiguration();

            var typeSpecDir = tempDir.CreateSubDirectory("typespec");
            File.WriteAllText(Path.Combine(typeSpecDir, "main.tsp"), "// TypeSpec content");
            var outputDir = tempDir.CreateSubDirectory("output");

            var task1 = Task.Run(() => config.ValidateInput(typeSpecDir, null, outputDir));
            var task2 = Task.Run(() => config.ValidateInput(typeSpecDir, null, outputDir));
            var task3 = Task.Run(() => config.ValidateInput(typeSpecDir, null, outputDir));

            Task.WaitAll(task1, task2, task3);

            Assert.That(task1.Result, Is.EqualTo(0));
            Assert.That(task2.Result, Is.EqualTo(0));
            Assert.That(task3.Result, Is.EqualTo(0));
        }

        #endregion
    }
}
