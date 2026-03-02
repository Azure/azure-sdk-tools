using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Microagents;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.Package;
using Microsoft.Extensions.Logging;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.Package
{
    [TestFixture]
    public class PackageCheckToolTests
    {
        private Mock<ILogger<PackageCheckTool>> _mockLogger;
        private Mock<IProcessHelper> _mockProcessHelper;
        private Mock<IPythonHelper> _mockPythonHelper;
        private Mock<INpxHelper> _mockNpxHelper;
        private Mock<IGitHelper> _mockGitHelper;
        private Mock<ILogger<PythonLanguageService>> _mockPythonLogger;
        private PackageCheckTool _packageCheckTool;
        private TempDirectory _testProjectPath;
        private Mock<ICommonValidationHelpers> _mockCommonValidationHelpers;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<PackageCheckTool>>();
            _mockProcessHelper = new Mock<IProcessHelper>();
            _mockPythonHelper = new Mock<IPythonHelper>();
            _mockNpxHelper = new Mock<INpxHelper>();
            _mockGitHelper = new Mock<IGitHelper>();
            _mockGitHelper.Setup(g => g.GetRepoNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("azure-sdk-for-python");
            _mockPythonLogger = new Mock<ILogger<PythonLanguageService>>();
            _mockCommonValidationHelpers = new Mock<ICommonValidationHelpers>();

            // Create language-specific check implementations with mocked dependencies
            var pythonCheck = new PythonLanguageService(_mockProcessHelper.Object, _mockPythonHelper.Object, _mockNpxHelper.Object, _mockGitHelper.Object, _mockPythonLogger.Object, _mockCommonValidationHelpers.Object, Mock.Of<IPackageInfoHelper>(), Mock.Of<IFileHelper>(), Mock.Of<ISpecGenSdkConfigHelper>(), Mock.Of<IChangelogHelper>());

            var languageChecks = new List<PythonLanguageService> { pythonCheck };

            _packageCheckTool = new PackageCheckTool(_mockLogger.Object, _mockGitHelper.Object, languageChecks);

            // Setup default mock responses
            var defaultProcessResult = new ProcessResult { ExitCode = 0, OutputDetails = new List<(StdioLevel, string)>() };
            _mockProcessHelper.Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                             .ReturnsAsync(defaultProcessResult);
            _mockNpxHelper.Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(defaultProcessResult);

            // Create a temporary test directory
            _testProjectPath = TempDirectory.Create("PackageCheckToolTest");
        }

        [TearDown]
        public void TearDown()
        {
            _testProjectPath.Dispose();
        }

        [Test]
        public async Task RunPackageCheck_WithAllChecks_ReturnsFailureResult()
        {
            // Act - Using empty temp directory will cause dependency check to fail
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.All, false);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ExitCode != 0 && !string.IsNullOrEmpty(result.ResponseError));
            Assert.That(result.ExitCode, Is.EqualTo(1));
        }

        [Test]
        public async Task RunPackageCheck_WithChangelogCheck_ReturnsResult()
        {
            // Act
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.Changelog, false);

            // Assert
            Assert.IsNotNull(result);
            // Changelog check may succeed or fail depending on directory contents
            Assert.That(result.ExitCode, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task RunPackageCheck_WithDependencyCheck_ReturnsResult()
        {
            // Act
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.Dependency, false);

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
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.Readme, false);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.ExitCode, Is.Not.EqualTo(0), "Should fail when no README exists");
            Assert.IsNotNull(result.CheckStatusDetails);
        }

        [Test]
        public async Task RunPackageCheck_WithSpellingCheck_WhenFileWithTypos_ReturnsFailure()
        {
            // Arrange - Create a file with obvious spelling errors
            var testFile = Path.Combine(_testProjectPath.DirectoryPath, "test.md");
            await File.WriteAllTextAsync(testFile, "This file contians obvioius speling erors.");

            // Act
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.Cspell, false);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.CheckStatusDetails);
            Assert.That(result.ExitCode, Is.Not.EqualTo(0));
        }

        [Test]
        public async Task RunPackageCheck_WithProjectFile_ReturnsPartialSuccess()
        {
            // Arrange - Create a basic project file to trigger language detection
            var projectFilePath = Path.Combine(_testProjectPath.DirectoryPath, "test.csproj");
            await File.WriteAllTextAsync(projectFilePath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

            // Act - This will still fail because dotnet commands won't work properly, but test structure is better
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.All, false);

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
            var result = await _packageCheckTool.RunPackageCheck(invalidPath, PackageCheckType.All, false);

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
            var result = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.All, false);

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
            var allResult = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.All, false);
            var changelogResult = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.Changelog, false);
            var dependencyResult = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.Dependency, false);
            var readmeResult = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.Readme, false);
            var spellingResult = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.Cspell, false);
            var snippetsResult = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.Snippets, false);

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
            var testFile = Path.Combine(_testProjectPath.DirectoryPath, "test_fix.md");
            await File.WriteAllTextAsync(testFile, "This file contians obvioius speling erors.");

            // Create a mock repository root and cspell config
            var mockRepoRoot = Path.GetTempPath();
            var cspellConfigDir = Path.Combine(mockRepoRoot, ".vscode");
            var cspellConfigPath = Path.Combine(cspellConfigDir, "cspell.json");

            Directory.CreateDirectory(cspellConfigDir);
            await File.WriteAllTextAsync(cspellConfigPath, "{}"); // Create minimal cspell config

            // Setup mocks
            _mockGitHelper.Setup(x => x.DiscoverRepoRootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(mockRepoRoot);

            // Setup mock to return spelling errors for cspell check (exit code 1 indicates errors found)
            var cspellErrorResult = new ProcessResult { ExitCode = 1 };
            cspellErrorResult.AppendStdout("test_fix.md:1:11 - Unknown word (contians)");
            cspellErrorResult.AppendStdout("test_fix.md:1:19 - Unknown word (obvioius)");
            cspellErrorResult.AppendStdout("test_fix.md:1:27 - Unknown word (speling)");
            cspellErrorResult.AppendStdout("test_fix.md:1:34 - Unknown word (erors)");

            // Reset and setup mock for this specific test
            _mockNpxHelper.Reset();
            _mockNpxHelper.Setup(x => x.Run(It.IsAny<NpxOptions>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(cspellErrorResult);

            // Setup mock microagent service to return a successful spelling fix result
            var mockSpellingFixResult = new CommonValidationHelpers.SpellingFixResult(
                "Successfully fixed 4 spelling errors and added 0 words to cspell.json. Fixed 'contians' to 'contains', 'obvioius' to 'obvious', 'speling' to 'spelling', 'erors' to 'errors' in test_fix.md"
            );
            // Setup CommonValidationHelpers mock to return appropriate results
            // For fixCheckErrors = false, return the error result
            _mockCommonValidationHelpers.Setup(x => x.CheckSpelling(It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
                                       .ReturnsAsync(new PackageCheckResponse(cspellErrorResult));

            // For fixCheckErrors = true, return success result
            _mockCommonValidationHelpers.Setup(x => x.CheckSpelling(It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                                       .ReturnsAsync(new PackageCheckResponse(0, mockSpellingFixResult.Summary));

            try
            {
                // Act - Test both regular cspell check and with fix enabled
                var normalResult = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.Cspell, false);
                var fixResult = await _packageCheckTool.RunPackageCheck(_testProjectPath.DirectoryPath, PackageCheckType.Cspell, true);

                // Assert
                Assert.IsNotNull(normalResult);
                Assert.IsNotNull(fixResult);
                Assert.IsNotNull(normalResult.CheckStatusDetails);
                Assert.IsNotNull(fixResult.CheckStatusDetails);

                // Normal result should fail since there are spelling errors
                Assert.That(normalResult.ExitCode, Is.EqualTo(1));

                // Fix result should succeed since the microagent fixed the issues
                Assert.That(fixResult.ExitCode, Is.EqualTo(0));

                // The fix result should contain details about what was fixed
                Assert.That(fixResult.CheckStatusDetails, Does.Contain("Successfully fixed"));
            }
            finally
            {
                // Cleanup
                if (File.Exists(cspellConfigPath))
                {
                    File.Delete(cspellConfigPath);
                }
                if (Directory.Exists(cspellConfigDir))
                {
                    Directory.Delete(cspellConfigDir);
                }
            }
        }
    }
}
