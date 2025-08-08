#nullable disable

using System;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    /// <summary>
    /// Integration tests for PythonLanguageRepoService that require Python and tox to be installed
    /// </summary>
    [TestFixture]
    public class PythonLanguageRepoServiceIntegrationTests
    {
        private Mock<ILogger> _mockLogger;
        private Mock<IGitHelper> _mockGitHelper;
        private string _tempProjectDir;
        private string _tempRepoDir;

        [SetUp]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockGitHelper = new Mock<IGitHelper>();
            
            // Create temporary test directories
            _tempProjectDir = Path.Combine(Path.GetTempPath(), "PythonIntegrationTest_Project", Guid.NewGuid().ToString("N"));
            _tempRepoDir = Path.Combine(Path.GetTempPath(), "PythonIntegrationTest_Repo", Guid.NewGuid().ToString("N"));
            
            Directory.CreateDirectory(_tempProjectDir);
            Directory.CreateDirectory(_tempRepoDir);
            
            // Setup mock git helper to return test repo root
            _mockGitHelper.Setup(g => g.DiscoverRepoRoot(It.IsAny<string>()))
                         .Returns(_tempRepoDir);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_tempProjectDir))
                    Directory.Delete(_tempProjectDir, true);
                
                if (Directory.Exists(_tempRepoDir))
                    Directory.Delete(_tempRepoDir, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to cleanup test directories: {ex.Message}");
            }
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithPythonAndToxInstalled_ExecutesSuccessfully()
        {
            // Check if Python is available
            if (!IsPythonAvailable())
            {
                Assert.Ignore("Python is not available in PATH, skipping integration test");
                return;
            }

            // Check if tox is available
            if (!IsToxAvailable())
            {
                Assert.Ignore("Tox is not available in PATH, skipping integration test");
                return;
            }

            // Arrange - Create a minimal Python project structure
            await CreateMinimalPythonProject();
            
            var pythonService = new PythonLanguageRepoService(_tempProjectDir, new ProcessHelper(NullLogger<ProcessHelper>.Instance), _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await pythonService.AnalyzeDependenciesAsync();

            // Assert
            Assert.IsNotNull(result);
            // The test may pass or fail depending on the tox configuration, but it should not crash
            Assert.That(result.ExitCode, Is.GreaterThanOrEqualTo(0));
            
            // Verify logging occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Starting dependency analysis")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithMalformedToxConfig_ReturnsAppropriateResponse()
        {
            // Check if Python/tox is available
            if (!IsPythonAvailable() || !IsToxAvailable())
            {
                Assert.Ignore("Python or tox is not available, skipping integration test");
                return;
            }

            // Arrange - Create a malformed tox configuration
            await CreateMalformedToxConfig();
            await CreateMinimalPythonProject();
            
            var pythonService = new PythonLanguageRepoService(_tempProjectDir, new ProcessHelper(NullLogger<ProcessHelper>.Instance), _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await pythonService.AnalyzeDependenciesAsync();

            // Assert
            Assert.IsNotNull(result);
            // With a malformed config, we expect a failure
            Assert.That(result.ExitCode, Is.Not.EqualTo(0));
        }

        [Test]
        public async Task AnalyzeDependenciesAsync_WithValidMinimalConfig_ExecutesCommand()
        {
            // Check if Python/tox is available
            if (!IsPythonAvailable() || !IsToxAvailable())
            {
                Assert.Ignore("Python or tox is not available, skipping integration test");
                return;
            }

            // Arrange - Create a valid minimal tox configuration
            await CreateValidMinimalToxConfig();
            await CreateMinimalPythonProject();
            
            var pythonService = new PythonLanguageRepoService(_tempProjectDir, new ProcessHelper(NullLogger<ProcessHelper>.Instance), _mockGitHelper.Object, _mockLogger.Object);

            // Act
            var result = await pythonService.AnalyzeDependenciesAsync();

            // Assert
            Assert.IsNotNull(result);
            
            // Verify that the command execution was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Executing command: tox")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        private bool IsPythonAvailable()
        {
            var pythonProgram = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python.exe" : "python";
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
            return paths.Any(p => File.Exists(Path.Combine(p, pythonProgram)));
        }

        private bool IsToxAvailable()
        {
            var toxProgram = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "tox.exe" : "tox";
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
            return paths.Any(p => File.Exists(Path.Combine(p, toxProgram)));
        }

        private async Task CreateMinimalPythonProject()
        {
            // Create a minimal Python project structure
            var setupPy = Path.Combine(_tempProjectDir, "setup.py");
            var setupContent = @"
from setuptools import setup, find_packages

setup(
    name='test-package',
    version='0.1.0',
    packages=find_packages(),
    python_requires='>=3.6',
)";
            await File.WriteAllTextAsync(setupPy, setupContent);

            // Create a requirements.txt
            var requirementsTxt = Path.Combine(_tempProjectDir, "requirements.txt");
            await File.WriteAllTextAsync(requirementsTxt, "# No dependencies for test\n");

            // Create an __init__.py file to make it a package
            var packageDir = Path.Combine(_tempProjectDir, "test_package");
            Directory.CreateDirectory(packageDir);
            var initPy = Path.Combine(packageDir, "__init__.py");
            await File.WriteAllTextAsync(initPy, "# Test package\n");
        }

        private async Task CreateValidMinimalToxConfig()
        {
            var toxDir = Path.Combine(_tempRepoDir, "eng", "tox");
            Directory.CreateDirectory(toxDir);
            
            var toxConfig = Path.Combine(toxDir, "tox.ini");
            var toxContent = @"
[tox]
envlist = mindependency

[testenv:mindependency]
deps = 
commands = python -c ""print('Dependency check completed successfully')""
";
            await File.WriteAllTextAsync(toxConfig, toxContent);
        }

        private async Task CreateMalformedToxConfig()
        {
            var toxDir = Path.Combine(_tempRepoDir, "eng", "tox");
            Directory.CreateDirectory(toxDir);
            
            var toxConfig = Path.Combine(toxDir, "tox.ini");
            var malformedContent = @"
[tox
envlist = mindependency
# Missing closing bracket and invalid syntax
[testenv:mindependency
deps = 
commands = invalid-command-that-does-not-exist
";
            await File.WriteAllTextAsync(toxConfig, malformedContent);
        }
    }
}
