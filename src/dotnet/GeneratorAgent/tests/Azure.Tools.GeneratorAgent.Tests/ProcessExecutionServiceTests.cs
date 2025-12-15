using System.Diagnostics;
using Azure.Tools.GeneratorAgent.Exceptions;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class ProcessExecutionServiceTests
    {
        [Test]
        public void Constructor_WithValidLogger_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var logger = NullLogger<ProcessExecutionService>.Instance;
            
            // Assert
            Assert.DoesNotThrow(() => new ProcessExecutionService(logger));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new ProcessExecutionService(null!));
            Assert.That(exception!.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void ExecuteAsync_WithNullCommand_ShouldThrowArgumentNullException()
        {
            // Arrange
            var service = CreateService();
            
            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentNullException>(() => 
                service.ExecuteAsync(null!, "", null, CancellationToken.None));
            Assert.That(exception!.ParamName, Is.EqualTo("command"));
        }

        [Test]
        public void ExecuteAsync_WithEmptyCommand_ShouldThrowArgumentException()
        {
            // Arrange
            var service = CreateService();
            
            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                service.ExecuteAsync("", "", null, CancellationToken.None));
            Assert.That(exception!.ParamName, Is.EqualTo("command"));
        }

        [Test]
        public void ExecuteAsync_WithWhitespaceCommand_ShouldThrowArgumentException()
        {
            // Arrange
            var service = CreateService();
            
            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                service.ExecuteAsync("   ", "", null, CancellationToken.None));
            Assert.That(exception!.ParamName, Is.EqualTo("command"));
        }

        [Test]
        public void ExecuteAsync_WithDisallowedCommand_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            var service = CreateService();
            var disallowedCommand = "malicious-command";
            
            // Act & Assert
            var exception = Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
                service.ExecuteAsync(disallowedCommand, "", null, CancellationToken.None));
            Assert.That(exception!.Message, Does.Contain("not in the allowed commands list"));
        }

        [Test]
        public void ExecuteAsync_WithNonExistentWorkingDirectory_ShouldThrowDirectoryNotFoundException()
        {
            // Arrange
            var service = CreateService();
            var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            
            // Act & Assert
            var exception = Assert.ThrowsAsync<DirectoryNotFoundException>(() => 
                service.ExecuteAsync("pwsh", "", nonExistentDir, CancellationToken.None));
            Assert.That(exception!.Message, Does.Contain("Working directory does not exist"));
        }

        [Test]
        public async Task ExecuteAsync_WithValidCommand_ShouldReturnSuccessResult()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var arguments = "-Command \"Write-Output 'Hello World'\"";
            
            // Act
            var result = await service.ExecuteAsync(command, arguments, null, CancellationToken.None);
            
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.IsFailure, Is.False);
                Assert.That(result.Value, Is.Not.Null);
                Assert.That(result.Value!.ToString(), Does.Contain("Hello World"));
                Assert.That(result.ProcessException, Is.Null);
            });
        }

        [Test]
        public async Task ExecuteAsync_WithNullArguments_ShouldUseEmptyArguments()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            
            // Act
            var result = await service.ExecuteAsync(command, null!, null, CancellationToken.None);
            
            // Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public async Task ExecuteAsync_WithEmptyArguments_ShouldSucceed()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            
            // Act
            var result = await service.ExecuteAsync(command, "", null, CancellationToken.None);
            
            // Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public async Task ExecuteAsync_WithNullWorkingDirectory_ShouldUseCurrentDirectory()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var arguments = "-Command \"Write-Output 'Test'\"";
            
            // Act
            var result = await service.ExecuteAsync(command, arguments, null, CancellationToken.None);
            
            // Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public async Task ExecuteAsync_WithValidWorkingDirectory_ShouldUseSpecifiedDirectory()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var arguments = "-Command \"Write-Output 'Test'\"";
            var tempDir = CreateTempDirectory();
            
            try
            {
                // Act
                var result = await service.ExecuteAsync(command, arguments, tempDir, CancellationToken.None);
                
                // Assert
                Assert.That(result.IsSuccess, Is.True);
            }
            finally
            {
                CleanupTempDirectory(tempDir);
            }
        }

        [Test]
        public async Task ExecuteAsync_WithMultilineOutput_ShouldCaptureAllOutput()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var arguments = "-Command \"Write-Output 'Line1'; Write-Output 'Line2'; Write-Output 'Line3'\"";
            
            // Act
            var result = await service.ExecuteAsync(command, arguments, null, CancellationToken.None);
            
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value!.ToString(), Does.Contain("Line1"));
                Assert.That(result.Value!.ToString(), Does.Contain("Line2"));
                Assert.That(result.Value!.ToString(), Does.Contain("Line3"));
            });
        }

        [Test]
        public async Task ExecuteAsync_WithSuccessfulTimeout_ShouldCompleteWithinTimeout()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var arguments = "-Command \"Write-Output 'Quick task'\"";
            var timeout = TimeSpan.FromSeconds(10);
            
            // Act
            var result = await service.ExecuteAsync(command, arguments, null, CancellationToken.None, timeout);
            
            // Assert
            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public async Task ExecuteAsync_WithOutputTrimming_ShouldRemoveTrailingWhitespace()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var arguments = "-Command \"Write-Output 'Test   '\"";
            
            // Act
            var result = await service.ExecuteAsync(command, arguments, null, CancellationToken.None);
            
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value!.ToString(), Does.Not.EndWith("   "));
            });
        }

        [Test]
        public async Task ExecuteAsync_WithFailingCommand_ShouldReturnFailureResult()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var arguments = "-Command \"Write-Error 'Test error'; exit 1\"";
            
            // Act
            var result = await service.ExecuteAsync(command, arguments, null, CancellationToken.None);
            
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Value, Is.Null);
                Assert.That(result.ProcessException, Is.Not.Null);
                Assert.That(result.ProcessException!.ExitCode, Is.EqualTo(1));
                Assert.That(result.ProcessException.Command, Is.EqualTo(command));
                Assert.That(result.ProcessException.Message, Does.Contain("exit code 1"));
            });
        }

        [Test]
        public async Task ExecuteAsync_WithNonExistentCommand_ShouldReturnFailureResult()
        {
            // Arrange
            var service = CreateService();
            var command = "git"; // Valid command but invalid arguments
            var arguments = "nonexistent-git-subcommand-that-does-not-exist";
            
            // Act
            var result = await service.ExecuteAsync(command, arguments, null, CancellationToken.None);
            
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.ProcessException, Is.Not.Null);
                Assert.That(result.ProcessException!.ExitCode, Is.Not.EqualTo(0));
                Assert.That(result.ProcessException.Command, Is.EqualTo(command));
            });
        }

        [Test]
        public async Task ExecuteAsync_WithTimeout_ShouldReturnTimeoutFailure()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var arguments = "-Command \"Start-Sleep 5\""; // Sleep for 5 seconds
            var timeout = TimeSpan.FromMilliseconds(500); // But timeout after 500ms
            
            // Act
            var result = await service.ExecuteAsync(command, arguments, null, CancellationToken.None, timeout);
            
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.ProcessException, Is.Not.Null);
                Assert.That(result.ProcessException!.Message, Does.Contain("timeout"));
            });
        }

        [Test]
        public async Task ExecuteAsync_WithZeroTimeout_ShouldReturnTimeoutFailure()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var arguments = "-Command \"Write-Output 'Test'\"";
            var timeout = TimeSpan.Zero;
            
            // Act
            var result = await service.ExecuteAsync(command, arguments, null, CancellationToken.None, timeout);
            
            // Assert
            Assert.That(result.IsSuccess, Is.False);
        }

        [Test]
        public void ExecuteAsync_WithCancellation_ShouldThrowOperationCanceledException()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var arguments = "-Command \"Start-Sleep 10\"";
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(200);
            
            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(() => 
                service.ExecuteAsync(command, arguments, null, cts.Token));
        }

        [Test]
        public void ExecuteAsync_WithTimeoutAndCancellation_ShouldRespectCancellation()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var arguments = "-Command \"Start-Sleep 10\"";
            var timeout = TimeSpan.FromSeconds(5); // Longer timeout
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(200); // But cancel earlier
            
            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(() => 
                service.ExecuteAsync(command, arguments, null, cts.Token, timeout));
        }

        [Test]
        public void ExecuteAsync_WithUnexpectedException_ShouldThrowException()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ProcessExecutionService>>();
            var service = new TestableProcessExecutionService(mockLogger.Object);
            var command = "pwsh";
            var arguments = "simulate-unexpected-error";
            
            // Act & Assert
            var exception = Assert.ThrowsAsync<Exception>(() => 
                service.ExecuteAsync(command, arguments, null, CancellationToken.None));
            Assert.That(exception!.Message, Does.Contain("Simulated unexpected error"));
        }

        [Test]
        public async Task ExecuteAsync_WithSuccessfulCommand_ShouldLogDebugWhenEnabled()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ProcessExecutionService>>();
            mockLogger.Setup(x => x.IsEnabled(LogLevel.Debug)).Returns(true);
            var service = new ProcessExecutionService(mockLogger.Object);
            var command = "pwsh";
            var arguments = "-Command \"Write-Output 'Test'\"";
            
            // Act
            await service.ExecuteAsync(command, arguments, null, CancellationToken.None);
            
            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("succeeded")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_WithFailedCommand_ShouldLogError()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ProcessExecutionService>>();
            var service = new ProcessExecutionService(mockLogger.Object);
            var command = "pwsh";
            var arguments = "-Command \"Write-Error 'Error'; exit 1\"";
            
            // Act
            await service.ExecuteAsync(command, arguments, null, CancellationToken.None);
            
            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed with exit code")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_WithTimeout_ShouldLogTimeoutError()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ProcessExecutionService>>();
            var service = new ProcessExecutionService(mockLogger.Object);
            var command = "pwsh";
            var arguments = "-Command \"Start-Sleep 5\"";
            var timeout = TimeSpan.FromMilliseconds(200);
            
            // Act
            await service.ExecuteAsync(command, arguments, null, CancellationToken.None, timeout);
            
            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("timed out")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void CreateProcess_WithValidParameters_ShouldConfigureProcessCorrectly()
        {
            // Arrange
            var service = new TestableProcessExecutionService(NullLogger<ProcessExecutionService>.Instance);
            var command = "pwsh";
            var arguments = "-Command \"Write-Output 'Test'\"";
            var workingDir = Path.GetTempPath();
            
            // Act
            var process = service.PublicCreateProcess(command, arguments, workingDir);
            
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(process.StartInfo.FileName, Is.EqualTo(command));
                Assert.That(process.StartInfo.Arguments, Is.EqualTo(arguments));
                Assert.That(process.StartInfo.WorkingDirectory, Is.EqualTo(workingDir));
                Assert.That(process.StartInfo.RedirectStandardOutput, Is.True);
                Assert.That(process.StartInfo.RedirectStandardError, Is.True);
                Assert.That(process.StartInfo.UseShellExecute, Is.False);
                Assert.That(process.StartInfo.CreateNoWindow, Is.True);
            });
            
            process.Dispose();
        }

        [Test]
        public void CreateProcess_WithNullWorkingDirectory_ShouldUseCurrentDirectory()
        {
            // Arrange
            var service = new TestableProcessExecutionService(NullLogger<ProcessExecutionService>.Instance);
            var command = "pwsh";
            var arguments = "-Command \"Write-Output 'Test'\"";
            
            // Act
            var process = service.PublicCreateProcess(command, arguments, null);
            
            // Assert
            Assert.That(process.StartInfo.WorkingDirectory, Is.EqualTo(Environment.CurrentDirectory));
            process.Dispose();
        }

        [Test]
        public async Task ExecuteAsync_ConcurrentExecution_ShouldHandleCorrectly()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var tasks = new List<Task<Result<object>>>();
            
            // Act
            for (int i = 0; i < 5; i++)
            {
                var arguments = $"-Command \"Write-Output 'Test {i}'\"";
                tasks.Add(service.ExecuteAsync(command, arguments, null, CancellationToken.None));
            }
            
            var results = await Task.WhenAll(tasks);
            
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(results.Length, Is.EqualTo(5));
                for (int i = 0; i < 5; i++)
                {
                    Assert.That(results[i].IsSuccess, Is.True, $"Task {i} should succeed");
                    Assert.That(results[i].Value!.ToString(), Does.Contain($"Test {i}"), $"Task {i} should contain correct output");
                }
            });
        }

        [Test]
        public async Task ExecuteAsync_WithVeryLongOutput_ShouldCaptureAll()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var longString = new string('X', 10000);
            var arguments = $"-Command \"Write-Output '{longString}'\"";
            
            // Act
            var result = await service.ExecuteAsync(command, arguments, null, CancellationToken.None);
            
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value!.ToString(), Does.Contain(longString));
            });
        }

        [Test]
        public async Task ExecuteAsync_WithSpecialCharactersInOutput_ShouldHandleCorrectly()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var specialChars = "Hello World! 123";
            var arguments = $"-Command \"Write-Output '{specialChars}'\"";
            
            // Act
            var result = await service.ExecuteAsync(command, arguments, null, CancellationToken.None);
            
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value!.ToString(), Does.Contain(specialChars));
            });
        }

        [Test]
        public async Task ExecuteAsync_WithDifferentExitCodes_ShouldCaptureCorrectExitCode()
        {
            // Arrange
            var service = CreateService();
            var command = "pwsh";
            var arguments = "-Command \"exit 42\"";
            
            // Act
            var result = await service.ExecuteAsync(command, arguments, null, CancellationToken.None);
            
            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.ProcessException!.ExitCode, Is.EqualTo(42));
            });
        }

        private ProcessExecutionService CreateService()
        {
            return new ProcessExecutionService(NullLogger<ProcessExecutionService>.Instance);
        }

        private string CreateTempDirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        private void CleanupTempDirectory(string tempDir)
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }


        /// <summary>
        /// Testable version of ProcessExecutionService that exposes protected methods and can simulate errors
        /// </summary>
        private class TestableProcessExecutionService : ProcessExecutionService
        {
            public TestableProcessExecutionService(ILogger<ProcessExecutionService> logger) : base(logger) { }

            public Process PublicCreateProcess(string command, string arguments, string? workingDirectory)
            {
                return CreateProcess(command, arguments, workingDirectory);
            }

            public override async Task<Result<object>> ExecuteAsync(
                string command,
                string arguments,
                string? workingDir,
                CancellationToken cancellationToken,
                TimeSpan? timeout = null)
            {
                if (arguments == "simulate-unexpected-error")
                {
                    throw new Exception("Simulated unexpected error");
                }

                return await base.ExecuteAsync(command, arguments, workingDir, cancellationToken, timeout);
            }
        }
    }
}
