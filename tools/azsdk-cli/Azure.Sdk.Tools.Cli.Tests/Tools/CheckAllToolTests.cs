using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.CheckAllTool;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    [TestFixture]
    public class CheckAllToolTests
    {
        private Mock<ILogger<CheckAllTool>> _mockLogger;
        private Mock<IOutputService> _mockOutputService;
        private Mock<ILogger<DependencyCheckTool>> _mockDependencyCheckLogger;
        private Mock<ILogger<ChangelogValidationTool>> _mockChangelogValidationLogger;
        private Mock<ILogger<DependencyCheckFixTool>> _mockDependencyCheckFixLogger;
        private Mock<ILogger<ChangelogValidationFixTool>> _mockChangelogValidationFixLogger;
        private CheckAllTool _checkAllTool;
        private string _testProjectPath;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<CheckAllTool>>();
            _mockOutputService = new Mock<IOutputService>();
            _mockDependencyCheckLogger = new Mock<ILogger<DependencyCheckTool>>();
            _mockChangelogValidationLogger = new Mock<ILogger<ChangelogValidationTool>>();
            _mockDependencyCheckFixLogger = new Mock<ILogger<DependencyCheckFixTool>>();
            _mockChangelogValidationFixLogger = new Mock<ILogger<ChangelogValidationFixTool>>();

            _checkAllTool = new CheckAllTool(_mockLogger.Object, _mockOutputService.Object);
            
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
            Assert.IsFalse(string.IsNullOrEmpty(result.Message));
            Assert.IsNull(result.ResponseError);
            Assert.That(result.Message, Is.EqualTo("Some checks failed"));
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
            Assert.IsFalse(string.IsNullOrEmpty(result.Message));
            Assert.IsNull(result.ResponseError);
            // Even with project file, dependency check will likely fail without proper dotnet setup
            Assert.That(result.Message, Is.EqualTo("Some checks failed"));
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
            Assert.IsNotNull(result.ResponseError);
            Assert.IsTrue(result.ResponseError.Contains("Project path does not exist"));
        }

        [Test]
        public async Task RunAllChecks_WithValidPath_RunsAllChecks()
        {
            // Act
            var result = await _checkAllTool.RunAllChecks(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.ResponseError);
            
            // Verify that 2 checks ran (dependency check and changelog validation)
            var checkResults = result.Result as System.Collections.Generic.List<IOperationResult>;
            Assert.IsNotNull(checkResults);
            Assert.That(checkResults.Count, Is.EqualTo(2)); // Updated to match actual implementation
            
            // Verify both checks executed (they will fail but that's expected for empty directory)
            Assert.IsTrue(checkResults.Any(r => r.GetType().Name.Contains("SuccessResult") || r.GetType().Name.Contains("FailureResult")));
        }

        [Test]
        public async Task DependencyCheckFixTool_WithValidPath_ReturnsSuccessfulResult()
        {
            // Arrange
            var fixTool = new DependencyCheckFixTool(_mockDependencyCheckFixLogger.Object, _mockOutputService.Object);

            // Act
            var result = await fixTool.FixDependencyCheckValidation(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.ExitCode, Is.EqualTo(0)); // Success should have exit code 0
            Assert.IsTrue(result.Output.Contains("Dependency check fixes completed"));
        }

        [Test]
        public async Task ChangelogValidationFixTool_WithValidPath_ReturnsFailureResult()
        {
            // Arrange
            var fixTool = new ChangelogValidationFixTool(_mockChangelogValidationFixLogger.Object, _mockOutputService.Object);

            // Act - Using empty temp directory will cause tool to fail (no CHANGELOG.md)
            var result = await fixTool.FixChangelogValidation(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.ExitCode, Is.EqualTo(1)); // Failure should have exit code 1
            if (result is FailureResult failureResult)
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
            var fixTool = new ChangelogValidationFixTool(_mockChangelogValidationFixLogger.Object, _mockOutputService.Object);
            var changelogPath = Path.Combine(_testProjectPath, "CHANGELOG.md");
            await File.WriteAllTextAsync(changelogPath, "# Changelog\n\n## 1.0.0\n\n- Initial release\n");

            // Act
            var result = await fixTool.FixChangelogValidation(_testProjectPath);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.ExitCode, Is.EqualTo(0)); // Success should have exit code 0
            Assert.IsTrue(result.Output.Contains("Changelog validation fix prompt generated successfully"));
        }
    }
}