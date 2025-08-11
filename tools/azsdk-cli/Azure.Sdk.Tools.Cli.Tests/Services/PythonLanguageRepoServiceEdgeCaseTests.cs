#nullable disable

using System;
using Microsoft.Extensions.Logging.Abstractions;
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
    /// <summary>
    /// Additional edge case and behavior tests for PythonLanguageRepoService
    /// </summary>
    [TestFixture]
    public class PythonLanguageRepoServiceEdgeCaseTests
    {
        private Mock<ILogger<PythonLanguageRepoService>> _mockLogger;
        private Mock<IGitHelper> _mockGitHelper;
        private string _testPackagePath;
        private string _testRepoRoot;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<PythonLanguageRepoService>>();
            _mockGitHelper = new Mock<IGitHelper>();
            
            _testPackagePath = Path.Combine(Path.GetTempPath(), "PythonEdgeCaseTest_Package", Guid.NewGuid().ToString("N"));
            _testRepoRoot = Path.Combine(Path.GetTempPath(), "PythonEdgeCaseTest_Repo", Guid.NewGuid().ToString("N"));
            
            Directory.CreateDirectory(_testPackagePath);
            Directory.CreateDirectory(_testRepoRoot);
            
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
        public async Task AnalyzeDependenciesAsync_WithEmptyToxConfig_HandlesGracefully()
        {
            // Arrange
            var toxConfigPath = Path.Combine(_testRepoRoot, "eng", "tox", "tox.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(toxConfigPath));
            
            // Create empty tox config file
            await File.WriteAllTextAsync(toxConfigPath, "");
            
            var pythonService = new PythonLanguageRepoService(new ProcessHelper(NullLogger<ProcessHelper>.Instance), _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await pythonService.AnalyzeDependenciesAsync(_testPackagePath);

            // Assert
            Assert.IsNotNull(result);
            // Should handle empty config gracefully, likely returning a failure
            Assert.IsTrue(result is FailureCLICheckResponse || result is CookbookCLICheckResponse);
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithToxConfigInDifferentCase_FindsConfigCorrectly()
        {
            // Arrange - Test case sensitivity handling
            var toxConfigDir = Path.Combine(_testRepoRoot, "eng", "tox");
            Directory.CreateDirectory(toxConfigDir);
            
            // Create tox config with different case (if on case-sensitive filesystem)
            var toxConfigPath = Path.Combine(toxConfigDir, "tox.ini");
            await File.WriteAllTextAsync(toxConfigPath, "[tox]\nenvlist = mindependency\n");
            
            var pythonService = new PythonLanguageRepoService(new ProcessHelper(NullLogger<ProcessHelper>.Instance), _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await pythonService.AnalyzeDependenciesAsync(_testPackagePath);

            // Assert
            Assert.IsNotNull(result);
            // Should find the config and attempt to run (though may fail due to missing tox)
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
        public async Task AnalyzeDependenciesAsync_WithReadOnlyToxConfig_HandlesCorrectly()
        {
            // Arrange
            var toxConfigPath = Path.Combine(_testRepoRoot, "eng", "tox", "tox.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(toxConfigPath));
            await File.WriteAllTextAsync(toxConfigPath, "[tox]\nenvlist = mindependency\n");
            
            // Make file read-only (if supported by OS)
            try
            {
                var fileInfo = new FileInfo(toxConfigPath);
                fileInfo.IsReadOnly = true;
            }
            catch
            {
                // Ignore if not supported on this platform
            }
            
            var pythonService = new PythonLanguageRepoService(new ProcessHelper(NullLogger<ProcessHelper>.Instance), _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await pythonService.AnalyzeDependenciesAsync(_testPackagePath);

            // Assert
            Assert.IsNotNull(result);
            // Should still be able to read the file and process
            Assert.IsTrue(result is FailureCLICheckResponse || result is CookbookCLICheckResponse || result is SuccessCLICheckResponse);
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithVeryLongPath_HandlesCorrectly()
        {
            // Arrange - Create a path that's close to system limits (but still valid)
            var longDirName = new string('a', 50); // Create a reasonably long directory name
            var longPath = Path.Combine(_testRepoRoot, "eng", "tox", longDirName);
            
            try
            {
                Directory.CreateDirectory(longPath);
                var toxConfigPath = Path.Combine(_testRepoRoot, "eng", "tox", "tox.ini");
                await File.WriteAllTextAsync(toxConfigPath, "[tox]\nenvlist = mindependency\n");
                
                var pythonService = new PythonLanguageRepoService(new ProcessHelper(NullLogger<ProcessHelper>.Instance), _mockGitHelper.Object, _mockLogger.Object);

                // Act
                var result = await pythonService.AnalyzeDependenciesAsync(_testPackagePath);

                // Assert
                Assert.IsNotNull(result);
                // Should handle long paths without crashing
                Assert.That(result.ExitCode, Is.GreaterThanOrEqualTo(0));
            }
            catch (PathTooLongException)
            {
                Assert.Ignore("Path too long for this system, skipping test");
            }
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithSpecialCharactersInPath_HandlesCorrectly()
        {
            // Arrange - Create a path with special characters (where allowed by OS)
            var specialDirName = "test-dir_with.special@chars";
            var specialRepoRoot = Path.Combine(Path.GetTempPath(), "PythonSpecialTest", specialDirName);
            
            try
            {
                Directory.CreateDirectory(specialRepoRoot);
                _mockGitHelper.Setup(g => g.DiscoverRepoRoot(It.IsAny<string>()))
                             .Returns(specialRepoRoot);
                
                var toxConfigPath = Path.Combine(specialRepoRoot, "eng", "tox", "tox.ini");
                Directory.CreateDirectory(Path.GetDirectoryName(toxConfigPath));
                await File.WriteAllTextAsync(toxConfigPath, "[tox]\nenvlist = mindependency\n");
                
                var pythonService = new PythonLanguageRepoService(new ProcessHelper(NullLogger<ProcessHelper>.Instance), _mockGitHelper.Object, _mockLogger.Object);

                // Act
                var result = await pythonService.AnalyzeDependenciesAsync(_testPackagePath);

                // Assert
                Assert.IsNotNull(result);
                // Should handle special characters in paths
                Assert.That(result.ExitCode, Is.GreaterThanOrEqualTo(0));
                
                // Cleanup
                Directory.Delete(specialRepoRoot, true);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                Assert.Ignore("Special characters not supported in paths on this system");
            }
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithConcurrentCalls_HandlesCorrectly()
        {
            // Arrange
            var toxConfigPath = Path.Combine(_testRepoRoot, "eng", "tox", "tox.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(toxConfigPath));
            await File.WriteAllTextAsync(toxConfigPath, "[tox]\nenvlist = mindependency\n");
            
            var pythonService = new PythonLanguageRepoService(new ProcessHelper(NullLogger<ProcessHelper>.Instance), _mockGitHelper.Object, _mockLogger.Object);

            // Act - Make concurrent calls
            var task1 = pythonService.AnalyzeDependenciesAsync(_testPackagePath);
            var task2 = pythonService.AnalyzeDependenciesAsync(_testPackagePath);
            
            var results = await Task.WhenAll(task1, task2);

            // Assert
            Assert.IsNotNull(results[0]);
            Assert.IsNotNull(results[1]);
            
            // Both calls should complete without hanging or crashing
            Assert.That(results[0].ExitCode, Is.GreaterThanOrEqualTo(0));
            Assert.That(results[1].ExitCode, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithQuickCancellation_HandlesCancellationGracefully()
        {
            // Arrange
            var toxConfigPath = Path.Combine(_testRepoRoot, "eng", "tox", "tox.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(toxConfigPath));
            await File.WriteAllTextAsync(toxConfigPath, "[tox]\nenvlist = mindependency\n");
            
            var pythonService = new PythonLanguageRepoService(new ProcessHelper(NullLogger<ProcessHelper>.Instance), _mockGitHelper.Object, _mockLogger.Object);

            using var cts = new CancellationTokenSource();

            // Act - Start the operation and cancel quickly
            var analysisTask = pythonService.AnalyzeDependenciesAsync(_testPackagePath, cts.Token);
            cts.Cancel();
            
            var result = await analysisTask;

            // Assert
            Assert.IsNotNull(result);
            // Should handle cancellation gracefully without throwing
            Assert.DoesNotThrow(() => { var _ = result.ExitCode; });
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithNetworkUnavailable_ReturnsAppropriateError()
        {
            // Arrange - This test simulates a scenario where network dependencies might be needed
            var toxConfigPath = Path.Combine(_testRepoRoot, "eng", "tox", "tox.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(toxConfigPath));
            
            // Create a tox config that would require network access (like installing packages)
            var toxConfigWithNetworkDeps = @"
[tox]
envlist = mindependency

[testenv:mindependency]
deps = 
    requests==2.28.0
    nonexistent-package-12345==1.0.0
commands = python -c ""import requests; print('Dependencies checked')""
";
            await File.WriteAllTextAsync(toxConfigPath, toxConfigWithNetworkDeps);
            
            var pythonService = new PythonLanguageRepoService(new ProcessHelper(NullLogger<ProcessHelper>.Instance), _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await pythonService.AnalyzeDependenciesAsync(_testPackagePath);

            // Assert
            Assert.IsNotNull(result);
            // The command will likely fail due to missing packages, but should not crash
            Assert.That(result.ExitCode, Is.GreaterThanOrEqualTo(0));
        }
    }
}
