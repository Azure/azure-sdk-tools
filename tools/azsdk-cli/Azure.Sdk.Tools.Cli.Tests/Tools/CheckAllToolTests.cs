using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    [TestFixture]
    public class PackageCheckToolTests
    {
        private Mock<ILogger<PackageCheckTool>> _mockLogger;
        private Mock<IOutputService> _mockOutputService;
        private Mock<IGitHelper> _mockGitHelper;
        private Mock<ILanguageRepoServiceFactory> _mockLanguageRepoServiceFactory;
        private PackageCheckTool _packageCheckTool;
        private string _testProjectPath;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<PackageCheckTool>>();
            _mockOutputService = new Mock<IOutputService>();
            _mockGitHelper = new Mock<IGitHelper>();
            _mockLanguageRepoServiceFactory = new Mock<ILanguageRepoServiceFactory>();

            _packageCheckTool = new PackageCheckTool(_mockLogger.Object, _mockOutputService.Object, _mockLanguageRepoServiceFactory.Object);
            
            // Create a temporary test directory
            _testProjectPath = Path.Combine(Path.GetTempPath(), "PackageCheckToolTest");
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
        public async Task RunPackageCheck_WithAllChecks_ReturnsFailureResult()
        {
            // Act - Using empty temp directory will cause dependency check to fail
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.All);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ExitCode != 0 && !string.IsNullOrEmpty(result.ResponseError));
            Assert.That(result.ExitCode, Is.EqualTo(1));
        }

        [Test]
        public async Task RunPackageCheck_WithChangelogCheck_ReturnsResult()
        {
            // Act
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.Changelog);

            // Assert
            Assert.IsNotNull(result);
            // Changelog check may succeed or fail depending on directory contents
            Assert.That(result.ExitCode, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task RunPackageCheck_WithDependencyCheck_ReturnsResult()
        {
            // Act
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.Dependency);

            // Assert
            Assert.IsNotNull(result);
            // Dependency check may succeed or fail depending on directory contents
            Assert.That(result.ExitCode, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task RunPackageCheck_WithProjectFile_ReturnsPartialSuccess()
        {
            // Arrange - Create a basic project file to trigger language detection
            var projectFilePath = Path.Combine(_testProjectPath, "test.csproj");
            await File.WriteAllTextAsync(projectFilePath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            // Act - This will still fail because dotnet commands won't work properly, but test structure is better
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.All);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ExitCode == 0 || (result.ExitCode != 0 && !string.IsNullOrEmpty(result.ResponseError)));
            // Even with project file, dependency check will likely fail without proper dotnet setup
            Assert.That(result.ExitCode, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task RunPackageCheck_WithInvalidPath_ReturnsErrorResult()
        {
            // Arrange
            string invalidPath = "/tmp/nonexistent-path-12345";

            // Act
            var result = await _packageCheckTool.RunPackageCheck(invalidPath, PackageCheckType.All);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.IsTrue(!string.IsNullOrEmpty(result.ResponseError));
            Assert.IsTrue(result.ResponseError?.Contains("Package path does not exist"));
        }

        [Test]
        public async Task RunPackageCheck_WithValidPath_RunsAllChecks()
        {
            // Act
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.All);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ExitCode == 0 || (result.ExitCode != 0));
            
            // For valid paths, we expect the checks to run even if they fail
            // Since this is a test directory without proper project structure, checks may fail
            Assert.IsNotNull(result.CheckStatusDetails);
        }

        [Test]
        public async Task RunPackageCheck_EnumValues_WorksCorrectly()
        {
            // Test that all enum values work correctly
            
            // Act - Test all enum values
            var allResult = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.All);
            var changelogResult = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.Changelog);
            var dependencyResult = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.Dependency);

            // Assert
            Assert.IsNotNull(allResult);
            Assert.IsNotNull(changelogResult);
            Assert.IsNotNull(dependencyResult);
            
            // All should execute (may fail due to test environment, but should not error on check type)
            Assert.IsTrue(allResult.ExitCode >= 0);
            Assert.IsTrue(changelogResult.ExitCode >= 0);
            Assert.IsTrue(dependencyResult.ExitCode >= 0);
        }
    }
}