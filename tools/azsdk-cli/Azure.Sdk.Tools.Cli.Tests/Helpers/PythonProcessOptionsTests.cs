// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    internal class PythonProcessOptionsTests
    {
        private const string VenvEnvironmentVariable = "AZSDKTOOLS_PYTHON_VENV_PATH";
        private TestLogger<PythonProcessOptions> logger;
        private string? originalVenvPath;

        [SetUp]
        public void Setup()
        {
            logger = new TestLogger<PythonProcessOptions>();
            // Save original environment variable value
            originalVenvPath = Environment.GetEnvironmentVariable(VenvEnvironmentVariable);
        }

        [TearDown]
        public void TearDown()
        {
            // Restore original environment variable value
            Environment.SetEnvironmentVariable(VenvEnvironmentVariable, originalVenvPath);
        }

        [Test]
        public void Constructor_CreatesOptionsWithResolvedExecutable()
        {
            // Arrange & Act
            var options = new PythonProcessOptions("python", ["--version"], logger);

            // Assert
            Assert.That(options, Is.Not.Null);
            Assert.That(options.Args, Is.Not.Null);
            
            // On Windows, args are wrapped with /C and command, on Unix they're direct
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.That(options.Args, Does.Contain("--version"));
            }
            else
            {
                Assert.That(options.Args, Has.Count.EqualTo(1));
                Assert.That(options.Args[0], Is.EqualTo("--version"));
            }
        }

        [Test]
        public void Constructor_WithWorkingDirectory_SetsWorkingDirectory()
        {
            // Arrange
            var workingDir = Path.GetTempPath();

            // Act
            var options = new PythonProcessOptions(
                "python",
                ["--version"],
                logger,
                workingDirectory: workingDir
            );

            // Assert
            Assert.That(options.WorkingDirectory, Is.EqualTo(workingDir));
        }

        [Test]
        public void Constructor_WithTimeout_SetsTimeout()
        {
            // Arrange
            var timeout = TimeSpan.FromMinutes(5);

            // Act
            var options = new PythonProcessOptions(
                "python",
                ["--version"],
                logger,
                timeout: timeout
            );

            // Assert
            Assert.That(options.Timeout, Is.EqualTo(timeout));
        }

        [Test]
        public void Constructor_WithLogOutputStream_SetsLogOutputStream()
        {
            // Act
            var options = new PythonProcessOptions(
                "python",
                ["--version"],
                logger,
                logOutputStream: false
            );

            // Assert
            Assert.That(options.LogOutputStream, Is.False);
        }

        [Test]
        public void ResolvePythonExecutable_WithoutVenvPath_ReturnsExecutableName()
        {
            // Arrange
            Environment.SetEnvironmentVariable(VenvEnvironmentVariable, null);

            // Act
            var result = PythonProcessOptions.ResolvePythonExecutable("python", logger);

            // Assert
            Assert.That(result, Is.EqualTo("python"));
        }

        [Test]
        public void ResolvePythonExecutable_WithEmptyVenvPath_ReturnsExecutableName()
        {
            // Arrange
            Environment.SetEnvironmentVariable(VenvEnvironmentVariable, "");

            // Act
            var result = PythonProcessOptions.ResolvePythonExecutable("pytest", logger);

            // Assert
            Assert.That(result, Is.EqualTo("pytest"));
        }

        [Test]
        public void ResolvePythonExecutable_WithNonExistentVenvPath_ThrowsDirectoryNotFoundException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Environment.SetEnvironmentVariable(VenvEnvironmentVariable, nonExistentPath);

            // Act & Assert
            var ex = Assert.Throws<DirectoryNotFoundException>(() =>
                PythonProcessOptions.ResolvePythonExecutable("python", logger));

            Assert.That(ex.Message, Does.Contain(VenvEnvironmentVariable));
            Assert.That(ex.Message, Does.Contain(nonExistentPath));
        }

        [Test]
        public void ResolvePythonExecutable_WithValidVenvPath_ResolvesCorrectly()
        {
            // Arrange
            var tempVenvPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                Directory.CreateDirectory(tempVenvPath);
                var binDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Scripts" : "bin";
                Directory.CreateDirectory(Path.Combine(tempVenvPath, binDir));
                
                Environment.SetEnvironmentVariable(VenvEnvironmentVariable, tempVenvPath);

                // Act
                var result = PythonProcessOptions.ResolvePythonExecutable("python", logger);

                // Assert
                var expectedPath = Path.Combine(tempVenvPath, binDir, "python");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    expectedPath += ".exe";
                }
                Assert.That(result, Is.EqualTo(expectedPath));
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempVenvPath))
                {
                    Directory.Delete(tempVenvPath, true);
                }
            }
        }

        [Test]
        [Platform("Win")]
        public void ResolvePythonExecutable_OnWindows_AddsExeExtension()
        {
            // Arrange
            var tempVenvPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                Directory.CreateDirectory(tempVenvPath);
                Directory.CreateDirectory(Path.Combine(tempVenvPath, "Scripts"));
                
                Environment.SetEnvironmentVariable(VenvEnvironmentVariable, tempVenvPath);

                // Act
                var result = PythonProcessOptions.ResolvePythonExecutable("pytest", logger);

                // Assert
                Assert.That(result, Does.EndWith(".exe"));
                Assert.That(result, Does.Contain("Scripts"));
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempVenvPath))
                {
                    Directory.Delete(tempVenvPath, true);
                }
            }
        }

        [Test]
        [Platform("Win")]
        public void ResolvePythonExecutable_OnWindows_WithExeExtension_DoesNotAddAnotherExtension()
        {
            // Arrange
            var tempVenvPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                Directory.CreateDirectory(tempVenvPath);
                Directory.CreateDirectory(Path.Combine(tempVenvPath, "Scripts"));
                
                Environment.SetEnvironmentVariable(VenvEnvironmentVariable, tempVenvPath);

                // Act
                var result = PythonProcessOptions.ResolvePythonExecutable("python.exe", logger);

                // Assert
                Assert.That(result, Does.EndWith(".exe"));
                Assert.That(result, Does.Not.Contain(".exe.exe"));
                Assert.That(result, Does.Contain("Scripts"));
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempVenvPath))
                {
                    Directory.Delete(tempVenvPath, true);
                }
            }
        }

        [Test]
        [Platform("Linux,MacOsX")]
        public void ResolvePythonExecutable_OnUnix_UsesBinDirectory()
        {
            // Arrange
            var tempVenvPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                Directory.CreateDirectory(tempVenvPath);
                Directory.CreateDirectory(Path.Combine(tempVenvPath, "bin"));
                
                Environment.SetEnvironmentVariable(VenvEnvironmentVariable, tempVenvPath);

                // Act
                var result = PythonProcessOptions.ResolvePythonExecutable("python", logger);

                // Assert
                Assert.That(result, Does.Contain("bin"));
                Assert.That(result, Does.Not.EndWith(".exe"));
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempVenvPath))
                {
                    Directory.Delete(tempVenvPath, true);
                }
            }
        }

        [Test]
        public void ResolvePythonExecutable_WithDifferentExecutableNames_ResolvesCorrectly()
        {
            // Arrange
            var tempVenvPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                Directory.CreateDirectory(tempVenvPath);
                var binDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Scripts" : "bin";
                Directory.CreateDirectory(Path.Combine(tempVenvPath, binDir));
                
                Environment.SetEnvironmentVariable(VenvEnvironmentVariable, tempVenvPath);

                var executables = new[] { "python", "pytest", "azpysdk", "pip" };

                // Act & Assert
                foreach (var executable in executables)
                {
                    var result = PythonProcessOptions.ResolvePythonExecutable(executable, logger);
                    Assert.That(result, Does.Contain(executable));
                    Assert.That(result, Does.Contain(tempVenvPath));
                }
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempVenvPath))
                {
                    Directory.Delete(tempVenvPath, true);
                }
            }
        }

        [Test]
        public void ResolvePythonExecutable_LogsResolution()
        {
            // Arrange
            var tempVenvPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                Directory.CreateDirectory(tempVenvPath);
                var binDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Scripts" : "bin";
                Directory.CreateDirectory(Path.Combine(tempVenvPath, binDir));
                
                Environment.SetEnvironmentVariable(VenvEnvironmentVariable, tempVenvPath);

                // Act
                var result = PythonProcessOptions.ResolvePythonExecutable("python", logger);

                // Assert - verify logger was used
                Assert.That(logger.Logs, Is.Not.Empty);
                Assert.That(logger.Logs.Any(m => m.ToString()!.Contains("python")), Is.True);
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempVenvPath))
                {
                    Directory.Delete(tempVenvPath, true);
                }
            }
        }

        [Test]
        public void ResolvePythonExecutable_WithNullLogger_DoesNotThrow()
        {
            // Arrange
            Environment.SetEnvironmentVariable(VenvEnvironmentVariable, null);

            // Act & Assert
            Assert.DoesNotThrow(() =>
                PythonProcessOptions.ResolvePythonExecutable("python", null));
        }

        [Test]
        public void Constructor_WithAllParameters_CreatesOptionsCorrectly()
        {
            // Arrange
            var args = new[] { "-m", "pytest", "--verbose" };
            var workingDir = Path.GetTempPath();
            var timeout = TimeSpan.FromMinutes(10);

            // Act
            var options = new PythonProcessOptions(
                "python",
                args,
                logger,
                workingDir,
                timeout,
                logOutputStream: false
            );

            // Assert
            // On Windows, args are wrapped with /C and command, on Unix they're direct
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.That(options.Args, Does.Contain("-m"));
                Assert.That(options.Args, Does.Contain("pytest"));
                Assert.That(options.Args, Does.Contain("--verbose"));
            }
            else
            {
                Assert.That(options.Args, Is.EqualTo(args));
            }
            Assert.That(options.WorkingDirectory, Is.EqualTo(workingDir));
            Assert.That(options.Timeout, Is.EqualTo(timeout));
            Assert.That(options.LogOutputStream, Is.False);
        }
    }
}
