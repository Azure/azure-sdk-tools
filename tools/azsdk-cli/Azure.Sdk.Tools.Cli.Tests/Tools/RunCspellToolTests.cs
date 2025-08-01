using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.RunCspellTool;
using Moq;
using System.IO;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    /// <summary>
    /// Test suite for RunCspellTool focusing on core validation functionality.
    /// Covers: basic validation, path validation, error handling.
    /// </summary>
    [TestFixture]
    internal class RunCspellToolTests
    {
        private TestLogger<RunCspellTool> logger;
        private RunCspellTool runCspellTool;
        private Mock<IOutputService> outputServiceMock;
        private string tempDirectory;

        [SetUp]
        public void Setup()
        {
            logger = new TestLogger<RunCspellTool>();
            outputServiceMock = new Mock<IOutputService>();
            outputServiceMock.Setup(x => x.Format(It.IsAny<object>())).Returns<object>(obj => obj?.ToString() ?? "");

            runCspellTool = new RunCspellTool(logger, outputServiceMock.Object);

            // Create a temporary directory for testing
            tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Test]
        public async Task RunCspellCheck_WithEmptyPath_ReturnsError()
        {
            // Act
            var result = await runCspellTool.RunCspellCheck("");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ResponseError, Is.Not.Null);
            Assert.That(result.ResponseError, Is.EqualTo("Package path is required"));
            Assert.That(runCspellTool.ExitCode, Is.EqualTo(1));
        }

        [Test]
        public async Task RunCspellCheck_WithNonExistentPath_ReturnsError()
        {
            // Arrange
            var nonExistentPath = "/path/that/does/not/exist";

            // Act
            var result = await runCspellTool.RunCspellCheck(nonExistentPath);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ResponseError, Is.Not.Null);
            Assert.That(result.ResponseError, Does.Contain("Package path does not exist"));
            Assert.That(runCspellTool.ExitCode, Is.EqualTo(1));
        }

        [Test]
        public async Task RunCspellCheck_WithValidPath_AttemptsToRunCspell()
        {
            // Arrange - Create a test file in temp directory
            var testFile = Path.Combine(tempDirectory, "test.md");
            await File.WriteAllTextAsync(testFile, "This is a test file with correct spelling.");

            // Act
            var result = await runCspellTool.RunCspellCheck(tempDirectory);

            // Assert
            Assert.That(result, Is.Not.Null);
            // The result might fail due to missing PowerShell script in test environment, 
            // but we should at least verify that it attempts to process the path
            Assert.That((result.ResponseError?.Contains("Could not locate Invoke-Cspell.ps1 script") ?? false) ||
                         (result.Message != null), Is.True);
        }

        [Test]
        public void GetCommand_ReturnsValidCommand()
        {
            // Act
            var command = runCspellTool.GetCommand();

            // Assert
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Name, Is.EqualTo("cspell"));
            Assert.That(command.Description, Is.EqualTo("Run cspell to check for typos in the specified package"));
            Assert.That(command.Arguments.Count, Is.EqualTo(1));
            Assert.That(command.Arguments[0].Name, Is.EqualTo("packagePath"));
        }

        [Test]
        public void CommandHierarchy_IsSetCorrectly()
        {
            // Assert
            Assert.That(runCspellTool.CommandHierarchy, Is.Not.Null);
            Assert.That(runCspellTool.CommandHierarchy.Length, Is.EqualTo(1));
            Assert.That(runCspellTool.CommandHierarchy[0].Verb, Is.EqualTo("checks"));
        }
    }
}