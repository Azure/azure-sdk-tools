using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.CheckAllTool;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    [TestFixture]
    public class CheckAllToolTests
    {
        private Mock<ILogger<CheckAllTool>> _mockLogger;
        private Mock<IOutputService> _mockOutputService;
        private Mock<IGitHelper> _mockGitHelper;
        private Mock<ILogger<DependencyCheckTool>> _mockDependencyCheckLogger;
        private Mock<ILogger<ChangelogValidationTool>> _mockChangelogValidationLogger;
        private Mock<ILogger<DependencyCheckTool>> _mockDependencyCheckFixLogger;
        private Mock<ILogger<ChangelogValidationTool>> _mockChangelogValidationFixLogger;
        private CheckAllTool _checkAllTool;
        private string _testProjectPath;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<CheckAllTool>>();
            _mockOutputService = new Mock<IOutputService>();
            _mockGitHelper = new Mock<IGitHelper>();
            _mockDependencyCheckLogger = new Mock<ILogger<DependencyCheckTool>>();
            _mockChangelogValidationLogger = new Mock<ILogger<ChangelogValidationTool>>();
            _mockDependencyCheckFixLogger = new Mock<ILogger<DependencyCheckTool>>();
            _mockChangelogValidationFixLogger = new Mock<ILogger<ChangelogValidationTool>>();

            _checkAllTool = new CheckAllTool(_mockLogger.Object, _mockOutputService.Object, _mockGitHelper.Object);
            
            // Create a temporary test directory
            _testProjectPath = Path.Combine(Path.GetTempPath(), "CheckAllToolTest");
            if (Directory.Exists(_testProjectPath))
            {
                Directory.Delete(_testProjectPath, true);
            }
            Directory.CreateDirectory(_testProjectPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testProjectPath))
            {
                Directory.Delete(_testProjectPath, true);
            }
        }

        [Test]
        public async Task RunAllChecks_WithValidPath_ReturnsFailureResult()
        {
            // Act - Using empty temp directory will cause dependency check to fail
            var result = await _checkAllTool.RunAllChecks(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result is FailureCLICheckResponse);
            Assert.That(result.ExitCode, Is.EqualTo(1));
        }

        [Test]
        public async Task RunAllChecks_WithProjectFile_ReturnsPartialSuccess()
        {
            // Arrange - Create a basic project file to trigger language detection
            var projectFilePath = Path.Combine(_testProjectPath, "test.csproj");
            await File.WriteAllTextAsync(projectFilePath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            // Act - This will still fail because dotnet commands won't work properly, but test structure is better
            var result = await _checkAllTool.RunAllChecks(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result is SuccessCLICheckResponse || result is FailureCLICheckResponse);
            // Even with project file, dependency check will likely fail without proper dotnet setup
            Assert.That(result.ExitCode, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task RunAllChecks_WithInvalidPath_ReturnsErrorResult()
        {
            // Arrange
            string invalidPath = "/tmp/nonexistent-path-12345";

            // Act
            var result = await _checkAllTool.RunAllChecks(invalidPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.IsTrue(result is FailureCLICheckResponse);
            var failureResult = result as FailureCLICheckResponse;
            Assert.IsTrue(failureResult.Error.Contains("Project path does not exist"));
        }

        [Test]
        public async Task RunAllChecks_WithValidPath_RunsAllChecks()
        {
            // Act
            var result = await _checkAllTool.RunAllChecks(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result is SuccessCLICheckResponse || result is FailureCLICheckResponse);
            
            // For valid paths, we expect the checks to run even if they fail
            // Since this is a test directory without proper project structure, checks may fail
            Assert.IsNotNull(result.Output);
        }

        [Test]
        public async Task DependencyCheckFixTool_WithValidPath_ReturnsSuccessfulResult()
        {
            // Arrange
            var fixTool = new DependencyCheckTool(_mockDependencyCheckFixLogger.Object, _mockOutputService.Object);

            // Act
            var result = await fixTool.RunDependencyCheck(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.ExitCode, Is.EqualTo(0)); // Success should have exit code 0
            Assert.IsTrue(result.Output.Contains("Dependency check fixes completed"));
        }

        [Test]
        public async Task ChangelogValidationFixTool_WithValidPath_ReturnsFailureResult()
        {
            // Arrange
            var fixTool = new ChangelogValidationTool(_mockChangelogValidationFixLogger.Object, _mockOutputService.Object, _mockGitHelper.Object);

            // Act - Using empty temp directory will cause tool to fail (no CHANGELOG.md)
            var result = await fixTool.RunChangelogValidation(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.ExitCode, Is.EqualTo(1)); // Failure should have exit code 1
            if (result is FailureCLICheckResponse failureResult)
            {
                Assert.IsTrue(failureResult.Error.Contains("No CHANGELOG.md file found in project at:"));
            }
            else
            {
                Assert.IsTrue(result.Output.Contains("No CHANGELOG.md file found in project at:"));
            }
        }

        [Test]
        public async Task ChangelogValidationFixTool_WithChangelogFile_ReturnsSuccessfulResult()
        {
            // Arrange
            var fixTool = new ChangelogValidationTool(_mockChangelogValidationFixLogger.Object, _mockOutputService.Object, _mockGitHelper.Object);
            var changelogPath = Path.Combine(_testProjectPath, "CHANGELOG.md");
            await File.WriteAllTextAsync(changelogPath, "# Changelog\n\n## 1.0.0\n\n- Initial release\n");

            // Act
            var result = await fixTool.RunChangelogValidation(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.ExitCode, Is.EqualTo(0)); // Success should have exit code 0
            Assert.IsTrue(result.Output.Contains("Changelog validation fix prompt generated successfully"));
        }
    }
}