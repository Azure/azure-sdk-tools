using System.CommandLine;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class CommandLineConfigurationTests
    {
        private sealed class TestDirectoryFixture : IDisposable
        {
            private readonly List<string> _createdDirectories = new();

            public string CreateDirectory(string baseName)
            {
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var directory = Path.Combine(Path.GetTempPath(), $"{baseName}_{uniqueId}");
                Directory.CreateDirectory(directory);
                _createdDirectories.Add(directory);
                return directory;
            }

            public void Dispose()
            {
                foreach (var directory in _createdDirectories)
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

        private Mock<ILogger<CommandLineConfiguration>> CreateMockLogger()
        {
            return new Mock<ILogger<CommandLineConfiguration>>();
        }

        private CommandLineConfiguration CreateCommandLineConfiguration(Mock<ILogger<CommandLineConfiguration>> mockLogger)
        {
            return new CommandLineConfiguration(mockLogger.Object);
        }

        private void VerifyLogError(Mock<ILogger<CommandLineConfiguration>> mockLogger, string expectedMessage)
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

        private void VerifyLogInformation(Mock<ILogger<CommandLineConfiguration>> mockLogger, string expectedMessage)
        {
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Test]
        public void Constructor_WithValidLogger_ShouldCreateInstance()
        {
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();

            CommandLineConfiguration config = CreateCommandLineConfiguration(mockLogger);

            Assert.That(config, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new CommandLineConfiguration(null!));

            Assert.That(exception?.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void CreateRootCommand_WithValidHandler_ShouldCreateRootCommandWithCorrectDescription()
        {
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            Task<int> MockHandler(string? a, string? b, string c) => Task.FromResult(0);

            RootCommand rootCommand = commandLineConfiguration.CreateRootCommand(MockHandler);

            Assert.That(rootCommand, Is.Not.Null);
            Assert.That(rootCommand.Description, Is.EqualTo("Azure SDK Generator Agent"));
        }

        [Test]
        public void CreateRootCommand_ShouldHaveCorrectNumberOfOptions()
        {
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            Task<int> MockHandler(string? a, string? b, string c) => Task.FromResult(0);

            RootCommand rootCommand = commandLineConfiguration.CreateRootCommand(MockHandler);

            Assert.That(rootCommand.Options.Count, Is.EqualTo(3));
        }

        [Test]
        public void CreateRootCommand_ShouldHaveTypespecPathOption()
        {
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            Task<int> MockHandler(string? a, string? b, string c) => Task.FromResult(0);

            RootCommand rootCommand = commandLineConfiguration.CreateRootCommand(MockHandler);

            Option? typespecPathOption = rootCommand.Options.FirstOrDefault(o => o.Name == "typespec-dir");
            Assert.That(typespecPathOption, Is.Not.Null);
            Assert.That(typespecPathOption?.Description, Is.EqualTo("Path to the local TypeSpec project directory or TypeSpec specification directory (e.g., specification/testservice/TestService)"));
            Assert.That(typespecPathOption?.IsRequired, Is.True);
        }

        [Test]
        public void CreateRootCommand_ShouldHaveCommitIdOption()
        {
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            Task<int> MockHandler(string? a, string? b, string c) => Task.FromResult(0);

            RootCommand rootCommand = commandLineConfiguration.CreateRootCommand(MockHandler);

            Option? commitIdOption = rootCommand.Options.FirstOrDefault(o => o.Name == "commit-id");
            Assert.That(commitIdOption, Is.Not.Null);
            Assert.That(commitIdOption?.Description, Is.EqualTo("GitHub commit ID to generate SDK from (optional, used with --typespec-dir for GitHub generation)"));
            Assert.That(commitIdOption?.IsRequired, Is.False);
        }

        [Test]
        public void CreateRootCommand_ShouldHaveSdkPathOption()
        {
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            Task<int> MockHandler(string? a, string? b, string c) => Task.FromResult(0);

            RootCommand rootCommand = commandLineConfiguration.CreateRootCommand(MockHandler);

            Option? sdkPathOption = rootCommand.Options.FirstOrDefault(o => o.Name == "output-dir");
            Assert.That(sdkPathOption, Is.Not.Null);
            Assert.That(sdkPathOption?.Description, Is.EqualTo("Output directory for generated SDK files"));
            Assert.That(sdkPathOption?.IsRequired, Is.True);
        }

        [Test]
        public void CreateRootCommand_ShouldSetHandlerCorrectly()
        {
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            Task<int> MockHandler(string? a, string? b, string c) => Task.FromResult(0);

            RootCommand rootCommand = commandLineConfiguration.CreateRootCommand(MockHandler);

            Assert.That(rootCommand.Handler, Is.Not.Null);
        }

        [Test]
        public void ValidateInput_WithValidTypespecPath_ShouldReturnSuccess()
        {
            using var fixture = new TestDirectoryFixture();
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            
            // Create temporary directories with valid TypeSpec files
            var typespecPath = fixture.CreateDirectory("ValidTypeSpec");
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");
            
            File.WriteAllText(Path.Combine(typespecPath, "test.tsp"), "// TypeSpec file");
            
            const string? commitId = null;

            int result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(0));
            VerifyLogInformation(mockLogger, "All input validation completed successfully");
        }

        [Test]
        public void ValidateInput_WithTypespecPathAndCommitId_ShouldReturnSuccess()
        {
            using var fixture = new TestDirectoryFixture();
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            
            // For GitHub paths (with commit ID), we don't need local validation
            const string typespecPath = "specification/testservice/TestService";
            const string commitId = "abc123";
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            int result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(0));
            VerifyLogInformation(mockLogger, "All input validation completed successfully");
        }

        [Test]
        public void ValidateInput_WithNullTypespecPath_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            const string? typespecPath = null;
            const string? commitId = null;
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            int result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "TypeSpec path cannot be null or empty");
        }

        [Test]
        public void ValidateInput_WithEmptyTypespecPath_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            const string typespecPath = "";
            const string? commitId = null;
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            int result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "TypeSpec path cannot be null or empty");
        }

        [Test]
        public void ValidateInput_WithWhitespaceTypespecPath_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            const string typespecPath = "   ";
            const string? commitId = null;
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            int result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "TypeSpec path cannot be null or empty");
        }

        [Test]
        public void ValidateInput_WithInvalidCommitId_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            const string typespecPath = "specification/testservice/TestService";
            const string commitId = "invalid-commit-id-with-special-chars!";
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            int result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "Commit ID must be 6-40 hexadecimal characters");
        }

        [Test]
        public void ValidateInput_WithInvalidOutputDirectory_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            
            // Create temporary directory with valid TypeSpec files
            var typespecPath = fixture.CreateDirectory("ValidTypeSpec");
            File.WriteAllText(Path.Combine(typespecPath, "test.tsp"), "// TypeSpec file");
            
            const string? commitId = null;
            const string sdkOutputPath = "../../../malicious/path"; // Path that will resolve to non-existent parent

            int result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "Parent directory does not exist");
        }

        [Test]
        public void ValidateInput_WithDirectoryTraversalInTypeSpecPath_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            const string typespecPath = "../../../malicious/typespec";
            const string? commitId = null;
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            int result = commandLineConfiguration.ValidateInput(typespecPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "TypeSpec directory not found");
        }

        [Test]
        public void ValidateInput_WithNonExistentLocalTypeSpecDirectory_ShouldReturnFailureAndLogError()
        {
            using var fixture = new TestDirectoryFixture();
            Mock<ILogger<CommandLineConfiguration>> mockLogger = CreateMockLogger();
            CommandLineConfiguration commandLineConfiguration = CreateCommandLineConfiguration(mockLogger);
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            const string? commitId = null;
            var sdkOutputPath = fixture.CreateDirectory("ValidOutput");

            int result = commandLineConfiguration.ValidateInput(nonExistentPath, commitId, sdkOutputPath);

            Assert.That(result, Is.EqualTo(1));
            VerifyLogError(mockLogger, "TypeSpec directory not found");
        }
    }
}
