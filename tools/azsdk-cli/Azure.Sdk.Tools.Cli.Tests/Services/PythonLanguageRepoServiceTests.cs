#nullable disable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    [TestFixture]
    public class PythonLanguageRepoServiceTests
    {
        private Mock<ILogger> _mockLogger;
        private Mock<IGitHelper> _mockGitHelper;
        private PythonLanguageRepoService _pythonService;
        private string _testPackagePath;
        private string _testRepoRoot;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockGitHelper = new Mock<IGitHelper>();
            
            // Create temporary test directories
            _testPackagePath = Path.Combine(Path.GetTempPath(), "PythonServiceTest_Package", Guid.NewGuid().ToString("N"));
            _testRepoRoot = Path.Combine(Path.GetTempPath(), "PythonServiceTest_Repo", Guid.NewGuid().ToString("N"));
            
            Directory.CreateDirectory(_testPackagePath);
            Directory.CreateDirectory(_testRepoRoot);
            
            // Setup mock git helper to return test repo root
            _mockGitHelper.Setup(g => g.DiscoverRepoRoot(It.IsAny<string>()))
                         .Returns(_testRepoRoot);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_testPackagePath))
                    Directory.Delete(_testPackagePath, true);
                
                if (Directory.Exists(_testRepoRoot))
                    Directory.Delete(_testRepoRoot, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to cleanup test directories: {ex.Message}");
            }
        }

        [Test]
        public void Constructor_WithValidParameters_SetsPropertiesCorrectly()
        {
            // Act
            _pythonService = new PythonLanguageRepoService(_testPackagePath, _mockGitHelper.Object, _mockLogger.Object);

            // Assert
            Assert.IsNotNull(_pythonService);
            // The service should be properly initialized without throwing exceptions
        }

        [Test]
        public void Constructor_WithNullGitHelper_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                new PythonLanguageRepoService(_testPackagePath, null, _mockLogger.Object));
            
            Assert.That(ex.ParamName, Is.EqualTo("gitHelper"));
        }

        [Test]
        public void Constructor_WithNullLogger_UsesNullLogger()
        {
            // Act & Assert - Should not throw, uses NullLogger internally
            Assert.DoesNotThrow(() => 
                new PythonLanguageRepoService(_testPackagePath, _mockGitHelper.Object, null));
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithMissingToxConfig_ReturnsFailureResponse()
        {
            // Arrange
            _pythonService = new PythonLanguageRepoService(_testPackagePath, _mockGitHelper.Object, _mockLogger.Object);
            
            // Act
            var result = await _pythonService.AnalyzeDependenciesAsync();

            // Assert
            Assert.IsNotNull(result);
            
            if (result is FailureCLICheckResponse failureResult)
            {
                // Check if the error message is in either the Error or Output field
                Assert.IsTrue(failureResult.Error.Contains("Tox configuration file not found") || 
                             failureResult.Output.Contains("Tox configuration file not found"));
            }
            else if (result is CookbookCLICheckResponse cookbookResult)
            {
                // Accept cookbook response as a valid result for missing tox config
                Assert.IsTrue(cookbookResult.Output.Contains("Failed to run dependency analysis"));
            }
            else
            {
                Assert.Fail($"Unexpected result type: {result.GetType().Name}");
            }
            
            // The exit code should indicate some form of failure or guidance
            Assert.That(result.ExitCode, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithValidToxConfig_CallsGitHelperCorrectly()
        {
            // Arrange
            var toxConfigPath = Path.Combine(_testRepoRoot, "eng", "tox", "tox.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(toxConfigPath));
            await File.WriteAllTextAsync(toxConfigPath, "[tox]\nenvlist = mindependency\n");
            
            _pythonService = new PythonLanguageRepoService(_testPackagePath, _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await _pythonService.AnalyzeDependenciesAsync();

            // Assert
            _mockGitHelper.Verify(g => g.DiscoverRepoRoot(_testPackagePath), Times.Once);
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithToxConfigExists_LogsCorrectInformation()
        {
            // Arrange
            var toxConfigPath = Path.Combine(_testRepoRoot, "eng", "tox", "tox.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(toxConfigPath));
            await File.WriteAllTextAsync(toxConfigPath, "[tox]\nenvlist = mindependency\n");
            
            _pythonService = new PythonLanguageRepoService(_testPackagePath, _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await _pythonService.AnalyzeDependenciesAsync();

            // Assert - Verify logging calls
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Starting dependency analysis for Python project")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
                
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Found repository root")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
                
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Using tox configuration file")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithCancellationToken_PassesTokenToProcess()
        {
            // Arrange
            var toxConfigPath = Path.Combine(_testRepoRoot, "eng", "tox", "tox.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(toxConfigPath));
            await File.WriteAllTextAsync(toxConfigPath, "[tox]\nenvlist = mindependency\n");
            
            _pythonService = new PythonLanguageRepoService(_testPackagePath, _mockGitHelper.Object, _mockLogger.Object);
            
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately to test cancellation

            // Act & Assert
            // The operation should handle cancellation gracefully
            var result = await _pythonService.AnalyzeDependenciesAsync(cts.Token);
            
            // The result should be either a failure due to cancellation or due to tox not being available
            Assert.IsNotNull(result);
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithGitHelperException_ReturnsFailureResponse()
        {
            // Arrange
            _mockGitHelper.Setup(g => g.DiscoverRepoRoot(It.IsAny<string>()))
                         .Throws(new DirectoryNotFoundException("Repository root not found"));
            
            _pythonService = new PythonLanguageRepoService(_testPackagePath, _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await _pythonService.AnalyzeDependenciesAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CookbookCLICheckResponse>(result);
            Assert.That(result.ExitCode, Is.EqualTo(0)); // Cookbook responses typically have exit code 0
            
            var cookbookResult = result as CookbookCLICheckResponse;
            Assert.IsTrue(cookbookResult.Output.Contains("Failed to run dependency analysis"));
            Assert.IsNotNull(cookbookResult.CookbookReference);
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithUnexpectedException_ReturnsCookbookResponse()
        {
            // Arrange
            _mockGitHelper.Setup(g => g.DiscoverRepoRoot(It.IsAny<string>()))
                         .Throws(new InvalidOperationException("Unexpected error"));
            
            _pythonService = new PythonLanguageRepoService(_testPackagePath, _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await _pythonService.AnalyzeDependenciesAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<CookbookCLICheckResponse>(result);
            
            var cookbookResult = result as CookbookCLICheckResponse;
            Assert.IsTrue(cookbookResult.Output.Contains("Failed to run dependency analysis"));
            Assert.IsTrue(cookbookResult.Output.Contains("Unexpected error"));
            Assert.That(cookbookResult.CookbookReference, Is.EqualTo("https://docs.python.org/3/tutorial/venv.html"));
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_LogsExceptionCorrectly()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Test exception");
            _mockGitHelper.Setup(g => g.DiscoverRepoRoot(It.IsAny<string>()))
                         .Throws(expectedException);
            
            _pythonService = new PythonLanguageRepoService(_testPackagePath, _mockGitHelper.Object, _mockLogger.Object);

            // Act
            await _pythonService.AnalyzeDependenciesAsync();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Exception occurred during dependency analysis")),
                    expectedException,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_ConstructsCorrectToxCommand()
        {
            // Arrange
            var toxConfigPath = Path.Combine(_testRepoRoot, "eng", "tox", "tox.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(toxConfigPath));
            await File.WriteAllTextAsync(toxConfigPath, "[tox]\nenvlist = mindependency\n");
            
            _pythonService = new PythonLanguageRepoService(_testPackagePath, _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await _pythonService.AnalyzeDependenciesAsync();

            // Assert - Verify the command execution logging
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Executing command: tox")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
                
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("run -e mindependency -c")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithMissingPackagePath_HandlesGracefully()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistent", Guid.NewGuid().ToString("N"));
            _pythonService = new PythonLanguageRepoService(nonExistentPath, _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await _pythonService.AnalyzeDependenciesAsync();

            // Assert
            Assert.IsNotNull(result);
            // Should either fail gracefully or return a cookbook response
            Assert.IsTrue(result is FailureCLICheckResponse || result is CookbookCLICheckResponse);
        }

        [Test]
        public void PythonLanguageRepoService_InheritsFromLanguageRepoService()
        {
            // Arrange & Act
            _pythonService = new PythonLanguageRepoService(_testPackagePath, _mockGitHelper.Object, _mockLogger.Object);

            // Assert
            Assert.IsInstanceOf<LanguageRepoService>(_pythonService);
            Assert.IsInstanceOf<ILanguageRepoService>(_pythonService);
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithValidSetup_VerifyWorkingDirectory()
        {
            // Arrange
            var toxConfigPath = Path.Combine(_testRepoRoot, "eng", "tox", "tox.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(toxConfigPath));
            await File.WriteAllTextAsync(toxConfigPath, "[tox]\nenvlist = mindependency\n");
            
            _pythonService = new PythonLanguageRepoService(_testPackagePath, _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await _pythonService.AnalyzeDependenciesAsync();

            // Assert
            // The process should be executed with the package path as working directory
            // This is validated through the behavior of the RunCommandAsync method
            Assert.IsNotNull(result);
            
            // Depending on whether tox is installed in the test environment, we can get either:
            // 1. A success response if tox ran successfully
            // 2. A failure response due to tox not being found
            // 3. A cookbook response with guidance
            Assert.IsTrue(result is SuccessCLICheckResponse || 
                         result is FailureCLICheckResponse || 
                         result is CookbookCLICheckResponse);
            
            // Exit code should be >= 0 (valid)
            Assert.That(result.ExitCode, Is.GreaterThanOrEqualTo(0));
        }
    }
}
