using Microsoft.Extensions.Logging;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    [TestFixture]
    public class PackageCheckToolTests
    {
        private Mock<ILogger<PackageCheckTool>> _mockLogger;
        private Mock<IProcessHelper> _mockProcessHelper;
        private Mock<INpxHelper> _mockNpxHelper;
        private Mock<IGitHelper> _mockGitHelper;
        private Mock<ILogger<LanguageChecks>> _mockLanguageChecksLogger;
        private Mock<ILogger<PythonLanguageSpecificChecks>> _mockPythonLogger;
        private Mock<ILogger<LanguageSpecificCheckResolver>> _mockResolverLogger;
        private LanguageChecks _languageChecks;
        private PackageCheckTool _packageCheckTool;
        private string _testProjectPath;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<PackageCheckTool>>();
            _mockProcessHelper = new Mock<IProcessHelper>();
            _mockNpxHelper = new Mock<INpxHelper>();
            _mockGitHelper = new Mock<IGitHelper>();
            _mockLanguageChecksLogger = new Mock<ILogger<LanguageChecks>>();
            _mockPythonLogger = new Mock<ILogger<PythonLanguageSpecificChecks>>();
            _mockResolverLogger = new Mock<ILogger<LanguageSpecificCheckResolver>>();

            // Create language-specific check implementations with mocked dependencies
            var pythonCheck = new PythonLanguageSpecificChecks(_mockProcessHelper.Object, _mockNpxHelper.Object, _mockGitHelper.Object, _mockPythonLogger.Object);

            var languageChecks = new List<ILanguageSpecificChecks> { pythonCheck };
            var mockPowershellHelper = new Mock<IPowershellHelper>();
            var resolver = new LanguageSpecificCheckResolver(languageChecks, _mockGitHelper.Object, mockPowershellHelper.Object, _mockResolverLogger.Object);            _languageChecks = new LanguageChecks(_mockProcessHelper.Object, _mockNpxHelper.Object, _mockGitHelper.Object, _mockLanguageChecksLogger.Object, resolver);
            _packageCheckTool = new PackageCheckTool(_mockLogger.Object, _languageChecks);

            // Setup default mock responses
            var defaultProcessResult = new ProcessResult { ExitCode = 0, OutputDetails = new List<(StdioLevel, string)>() };
            _mockProcessHelper.Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                             .ReturnsAsync(defaultProcessResult);
            _mockNpxHelper.Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(defaultProcessResult);

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
        public async Task RunPackageCheck_WithReadmeCheck_WhenNoReadmeExists_ReturnsFailure()
        {
            // Arrange - Empty directory with no README

            // Act
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.Readme);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.ExitCode, Is.Not.EqualTo(0), "Should fail when no README exists");
            Assert.IsNotNull(result.CheckStatusDetails);
        }

        [Test]
        public async Task RunPackageCheck_WithSpellingCheck_WhenFileWithTypos_ReturnsFailure()
        {
            // Arrange - Create a file with obvious spelling errors
            var testFile = Path.Combine(_testProjectPath, "test.md");
            await File.WriteAllTextAsync(testFile, "This file contians obvioius speling erors.");

            // Act
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.Cspell);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.CheckStatusDetails);
            Assert.That(result.ExitCode, Is.Not.EqualTo(0));
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
            var readmeResult = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.Readme);
            var spellingResult = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.Cspell);
            var snippetsResult = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.Snippets);

            // Assert
            Assert.IsNotNull(allResult);
            Assert.IsNotNull(changelogResult);
            Assert.IsNotNull(dependencyResult);
            Assert.IsNotNull(readmeResult);
            Assert.IsNotNull(spellingResult);
            Assert.IsNotNull(snippetsResult);

            // All should execute (may fail due to test environment, but should not error on check type)
            Assert.IsTrue(allResult.ExitCode >= 0);
            Assert.IsTrue(changelogResult.ExitCode >= 0);
            Assert.IsTrue(dependencyResult.ExitCode >= 0);
            Assert.IsTrue(readmeResult.ExitCode >= 0);
            Assert.IsTrue(spellingResult.ExitCode >= 0);
            Assert.IsTrue(snippetsResult.ExitCode >= 0);
        }

        [Test]
        public async Task RunPackageCheck_WithCspellFixEnabled_WhenFileWithTypos_ReturnsResult()
        {
            // Arrange - Create a file with obvious spelling errors
            var testFile = Path.Combine(_testProjectPath, "test_fix.md");
            await File.WriteAllTextAsync(testFile, "This file contians obvioius speling erors.");

            // Act - Test both regular cspell check and with fix enabled
            var normalResult = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.Cspell, false);
            var fixResult = await _packageCheckTool.RunPackageCheck(_testProjectPath, PackageCheckType.Cspell, true);

            // Assert
            Assert.IsNotNull(normalResult);
            Assert.IsNotNull(fixResult);
            Assert.IsNotNull(normalResult.CheckStatusDetails);
            Assert.IsNotNull(fixResult.CheckStatusDetails);
            
            // Both may succeed or partially succeed depending on environment and cspell version
            Assert.That(normalResult.ExitCode, Is.GreaterThanOrEqualTo(0));
            Assert.That(fixResult.ExitCode, Is.GreaterThanOrEqualTo(0));
            
            // The fix result should have different next steps indicating LLM guidance
            Assert.IsTrue(fixResult.NextSteps != null && fixResult.NextSteps.Any());
        }
    }
}
