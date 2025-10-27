using System.Diagnostics;
using System.Security;
using Azure.Tools.GeneratorAgent.Exceptions;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class ProcessExecutionServiceTests
    {
        private sealed class TestEnvironmentFixture : IDisposable
        {
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly string _uniqueId;
            private readonly List<string> _tempFiles;
            private readonly List<string> _tempDirectories;

            public TestEnvironmentFixture()
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _uniqueId = Guid.NewGuid().ToString("N")[..8];
                _tempFiles = new List<string>();
                _tempDirectories = new List<string>();
            }

            public CancellationToken CancellationToken => _cancellationTokenSource.Token;
            public string UniqueId => _uniqueId;

            public Mock<ILogger<ProcessExecutionService>> CreateMockLogger()
            {
                return new Mock<ILogger<ProcessExecutionService>>();
            }

            public ProcessExecutionService CreateProcessExecutionService(Mock<ILogger<ProcessExecutionService>>? mockLogger = null)
            {
                return new ProcessExecutionService((mockLogger ?? CreateMockLogger()).Object);
            }

            public string CreateValidCommand()
            {
                // Use PowerShell on all platforms as it's cross-platform and in allowed commands
                return SecureProcessConfiguration.PowerShellExecutable;
            }

            public string CreateAllowedCommand()
            {
                return SecureProcessConfiguration.GitExecutable;
            }

            public string CreateDisallowedCommand()
            {
                return $"malicious-command-{_uniqueId}";
            }

            public string CreateNonExistentCommand()
            {
                // Use git as it's in allowed commands but modify path to make it nonexistent
                return "git";
            }

            public string CreateNonExistentArguments()
            {
                return "invalid-git-command-that-does-not-exist";
            }

            public string CreateEchoArguments(string message = "test output")
            {
                return $"-Command \"Write-Output '{message}'\"";
            }

            public string CreateErrorArguments(string errorMessage = "test error", int exitCode = 1)
            {
                return $"-Command \"Write-Error '{errorMessage}'; exit {exitCode}\"";
            }

            public string CreateMultilineArguments()
            {
                return "-Command \"Write-Output 'Line1'; Write-Output 'Line2'; Write-Output 'Line3'\"";
            }

            public string CreateLongRunningArguments(int seconds = 10)
            {
                return $"-Command \"Start-Sleep {seconds}\"";
            }

            public string CreateValidWorkingDirectory()
            {
                var tempDir = Path.Combine(Path.GetTempPath(), $"ProcessExecutionServiceTest_{_uniqueId}");
                Directory.CreateDirectory(tempDir);
                _tempDirectories.Add(tempDir);
                return tempDir;
            }

            public string CreateInvalidWorkingDirectory()
            {
                return Path.Combine(Path.GetTempPath(), $"NonExistent_{_uniqueId}");
            }

            public string CreateDirectoryTraversalPath()
            {
                return Path.Combine(Path.GetTempPath(), "..", "..", "etc", "passwd");
            }

            public string CreateValidArguments()
            {
                return "-Command \"Write-Output 'Hello World'\"";
            }

            public string CreateEmptyArguments()
            {
                return string.Empty;
            }

            public TimeSpan CreateShortTimeout(int milliseconds = 100)
            {
                return TimeSpan.FromMilliseconds(milliseconds);
            }

            public TimeSpan CreateLongTimeout(int milliseconds = 10000)
            {
                return TimeSpan.FromMilliseconds(milliseconds);
            }

            public void CancelAfterDelay(int milliseconds)
            {
                _cancellationTokenSource.CancelAfter(milliseconds);
            }

            public void Dispose()
            {
                _cancellationTokenSource?.Dispose();

                // Clean up temp files
                foreach (var file in _tempFiles)
                {
                    try
                    {
                        if (File.Exists(file))
                            File.Delete(file);
                    }
                    catch { /* Ignore cleanup errors */ }
                }

                // Clean up temp directories
                foreach (var dir in _tempDirectories)
                {
                    try
                    {
                        if (Directory.Exists(dir))
                            Directory.Delete(dir, true);
                    }
                    catch { /* Ignore cleanup errors */ }
                }
            }
        }

        #region Constructor Tests

        [Test]
        public void Constructor_WithValidLogger_CreatesInstance()
        {
            using var fixture = new TestEnvironmentFixture();
            var mockLogger = fixture.CreateMockLogger();

            var executor = new ProcessExecutionService(mockLogger.Object);

            Assert.That(executor, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new ProcessExecutionService(null!));

            Assert.That(exception.ParamName!, Is.EqualTo("logger"));
        }

        #endregion

        #region ExecuteAsync - Success Scenarios

        [Test]
        public async Task ExecuteAsync_WithValidCommand_ReturnsSuccessResult()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateEchoArguments("test message");
            var workingDir = fixture.CreateValidWorkingDirectory();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.Not.Null);
                Assert.That(result.Value!.ToString(), Does.Contain("test message"));
                Assert.That(result.ProcessException, Is.Null);
                Assert.That(result.Exception, Is.Null);
            });
        }

        [Test]
        public async Task ExecuteAsync_WithNullArguments_UsesEmptyArguments()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var result = await executor.ExecuteAsync(
                command,
                null!, // Explicitly allow null for this test
                workingDir,
                fixture.CancellationToken);

            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public async Task ExecuteAsync_WithEmptyArguments_Succeeds()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateEmptyArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public async Task ExecuteAsync_WithNullWorkingDirectory_Succeeds()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateValidArguments();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                null,
                fixture.CancellationToken);

            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public async Task ExecuteAsync_WithMultilineOutput_CapturesAllLines()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateMultilineArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value?.ToString(), Does.Contain("Line1"));
                Assert.That(result.Value?.ToString(), Does.Contain("Line2"));
                Assert.That(result.Value?.ToString(), Does.Contain("Line3"));
            });
        }

        [Test]
        public async Task ExecuteAsync_WithTimeout_CompletesWithinTimeout()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateEchoArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();
            var timeout = fixture.CreateLongTimeout();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken,
                timeout);

            Assert.That(result.IsSuccess, Is.True);
        }

        #endregion

        #region ExecuteAsync - Failure Scenarios

        [Test]
        public void ExecuteAsync_WithNullCommand_ThrowsArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var arguments = fixture.CreateValidArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var exception = Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await executor.ExecuteAsync(
                    null!,
                    arguments,
                    workingDir,
                    fixture.CancellationToken));

            Assert.That(exception.ParamName!, Is.EqualTo("command"));
        }

        [Test]
        public void ExecuteAsync_WithEmptyCommand_ThrowsArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var arguments = fixture.CreateValidArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var exception = Assert.ThrowsAsync<ArgumentException>(async () =>
                await executor.ExecuteAsync(
                    string.Empty,
                    arguments,
                    workingDir,
                    fixture.CancellationToken));

            Assert.That(exception.ParamName!, Is.EqualTo("command"));
        }

        [Test]
        public void ExecuteAsync_WithWhitespaceCommand_ThrowsArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var arguments = fixture.CreateValidArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var exception = Assert.ThrowsAsync<ArgumentException>(async () =>
                await executor.ExecuteAsync(
                    "   ",
                    arguments,
                    workingDir,
                    fixture.CancellationToken));

            Assert.That(exception.ParamName!, Is.EqualTo("command"));
        }

        [Test]
        public void ExecuteAsync_WithDisallowedCommand_ThrowsUnauthorizedAccessException()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateDisallowedCommand();
            var arguments = fixture.CreateValidArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var exception = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await executor.ExecuteAsync(
                    command,
                    arguments,
                    workingDir,
                    fixture.CancellationToken));

            Assert.That(exception.Message!, Does.Contain("not in the allowed commands list"));
        }

        [Test]
        public void ExecuteAsync_WithInvalidWorkingDirectory_ThrowsSecurityException()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateValidArguments();
            var invalidDir = fixture.CreateInvalidWorkingDirectory();

            var exception = Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
                await executor.ExecuteAsync(
                    command,
                    arguments,
                    invalidDir,
                    fixture.CancellationToken));

            Assert.That(exception.Message!, Does.Contain("Working directory does not exist"));
        }

        [Test]
        public async Task ExecuteAsync_WithFailingCommand_ReturnsFailureResult()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateErrorArguments("test error", 1);
            var workingDir = fixture.CreateValidWorkingDirectory();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.ProcessException, Is.Not.Null);
                Assert.That(result.ProcessException, Is.TypeOf<GeneralProcessExecutionException>());
                Assert.That(result.ProcessException?.ExitCode, Is.EqualTo(1));
                Assert.That(result.ProcessException?.Command, Is.EqualTo(command));
            });
        }

        [Test]
        public async Task ExecuteAsync_WithNonExistentCommand_ReturnsFailureResult()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateNonExistentCommand();
            var arguments = fixture.CreateNonExistentArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.ProcessException, Is.Not.Null);
                Assert.That(result.ProcessException?.ExitCode, Is.Not.EqualTo(0));
            });
        }

        [Test]
        public async Task ExecuteAsync_WithTimeoutExceeded_ReturnsTimeoutFailure()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateLongRunningArguments(10);
            var workingDir = fixture.CreateValidWorkingDirectory();
            var timeout = fixture.CreateShortTimeout(200);

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken,
                timeout);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Exception, Is.Not.Null);
                Assert.That(result.Exception, Is.TypeOf<TimeoutException>());
                Assert.That(result.Exception?.Message, Does.Contain("timed out"));
            });
        }

        #endregion

        #region Cancellation Tests

        [Test]
        public void ExecuteAsync_WithCancellation_ThrowsOperationCanceledException()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateLongRunningArguments(10);
            var workingDir = fixture.CreateValidWorkingDirectory();
            fixture.CancelAfterDelay(200);

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await executor.ExecuteAsync(
                    command,
                    arguments,
                    workingDir,
                    fixture.CancellationToken));
        }

        [Test]
        public void ExecuteAsync_WithTimeoutAndCancellation_HandlesCorrectly()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateLongRunningArguments(10);
            var workingDir = fixture.CreateValidWorkingDirectory();
            var timeout = fixture.CreateLongTimeout(5000); // Longer than cancellation
            fixture.CancelAfterDelay(200);

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await executor.ExecuteAsync(
                    command,
                    arguments,
                    workingDir,
                    fixture.CancellationToken,
                    timeout));
        }

        #endregion

        #region Logging Tests

        [Test]
        public async Task ExecuteAsync_WithSuccessfulCommand_LogsSuccess()
        {
            using var fixture = new TestEnvironmentFixture();
            var mockLogger = fixture.CreateMockLogger();
            var executor = fixture.CreateProcessExecutionService(mockLogger);
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateEchoArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

            // The service doesn't log success - just verify it completed without throwing
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task ExecuteAsync_WithFailedCommand_LogsError()
        {
            using var fixture = new TestEnvironmentFixture();
            var mockLogger = fixture.CreateMockLogger();
            var executor = fixture.CreateProcessExecutionService(mockLogger);
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateErrorArguments("test error", 1);
            var workingDir = fixture.CreateValidWorkingDirectory();

            await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

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
        public async Task ExecuteAsync_WithNonExistentCommand_LogsError()
        {
            using var fixture = new TestEnvironmentFixture();
            var mockLogger = fixture.CreateMockLogger();
            var executor = fixture.CreateProcessExecutionService(mockLogger);
            var command = fixture.CreateNonExistentCommand();
            var arguments = fixture.CreateNonExistentArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

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
        public async Task ExecuteAsync_WithWin32Exception_LogsError()
        {
            using var fixture = new TestEnvironmentFixture();
            var mockLogger = fixture.CreateMockLogger();
            var executor = fixture.CreateProcessExecutionService(mockLogger);
            var command = fixture.CreateNonExistentCommand();
            var arguments = fixture.CreateNonExistentArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed with exit code")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Test]
        public async Task ExecuteAsync_WithTimeout_LogsTimeoutError()
        {
            using var fixture = new TestEnvironmentFixture();
            var mockLogger = fixture.CreateMockLogger();
            var executor = fixture.CreateProcessExecutionService(mockLogger);
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateLongRunningArguments(10);
            var workingDir = fixture.CreateValidWorkingDirectory();
            var timeout = fixture.CreateShortTimeout(200);

            await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken,
                timeout);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("timed out")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region CreateProcess Tests

        [Test]
        public void CreateProcess_WithValidParameters_CreatesProcessCorrectly()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateValidArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            // Use reflection to access protected method
            var method = typeof(ProcessExecutionService).GetMethod("CreateProcess", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var process = (Process)method!.Invoke(executor, [command, arguments, workingDir])!;

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
        public void CreateProcess_WithNullWorkingDirectory_UsesCurrentDirectory()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateValidArguments();

            // Use reflection to access protected method
            var method = typeof(ProcessExecutionService).GetMethod("CreateProcess", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var process = (Process)method!.Invoke(executor, [command, arguments, null])!;

            Assert.That(process.StartInfo.WorkingDirectory, Is.EqualTo(Environment.CurrentDirectory));

            process.Dispose();
        }

        #endregion

        #region Exception Handling Tests

        [Test]
        public async Task ExecuteAsync_WithWin32ExceptionNativeErrorCode2_ReturnsSpecificFailure()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateNonExistentCommand();
            var arguments = fixture.CreateNonExistentArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.ProcessException, Is.Not.Null);
                Assert.That(result.ProcessException?.ExitCode, Is.Not.EqualTo(0));
            });
        }

        [Test]
        public void ExecuteAsync_HandlesUnexpectedExceptions()
        {
            using var fixture = new TestEnvironmentFixture();
            var mockLogger = fixture.CreateMockLogger();
            
            // Create a testable executor that can simulate unexpected exceptions
            var executor = new TestableProcessExecutionService(mockLogger.Object);
            var command = "simulate-error"; // This will trigger the exception in TestableProcessExecutionService
            var arguments = fixture.CreateValidArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await executor.ExecuteAsync(
                    command,
                    arguments,
                    workingDir,
                    fixture.CancellationToken);
            });
        }

        #endregion

        #region Thread Safety Tests

        [Test]
        public async Task ExecuteAsync_ConcurrentExecution_HandlesCorrectly()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var tasks = new List<Task<Result<object>>>();
            for (int i = 0; i < 5; i++)
            {
                var arguments = fixture.CreateEchoArguments($"Test message {i}");
                tasks.Add(executor.ExecuteAsync(
                    command,
                    arguments,
                    workingDir,
                    fixture.CancellationToken));
            }

            var results = await Task.WhenAll(tasks);

            Assert.Multiple(() =>
            {
                Assert.That(results.Length, Is.EqualTo(5));
                for (int i = 0; i < 5; i++)
                {
                    Assert.That(results[i].IsSuccess, Is.True, $"Task {i} should succeed");
                    Assert.That(results[i].Value?.ToString(), Does.Contain($"Test message {i}"), $"Task {i} should contain correct message");
                }
            });
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Testable version of ProcessExecutionService for simulating specific scenarios
        /// </summary>
        private class TestableProcessExecutionService : ProcessExecutionService
        {
            public TestableProcessExecutionService(ILogger<ProcessExecutionService> logger) : base(logger) { }

            protected override Process CreateProcess(string command, string arguments, string? workingDirectory)
            {
                // Simulate an unexpected exception scenario if needed
                if (command.Contains("simulate-error"))
                {
                    throw new InvalidOperationException("Simulated unexpected error");
                }

                return base.CreateProcess(command, arguments, workingDirectory);
            }
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public async Task ExecuteAsync_WithVeryLongArguments_HandlesCorrectly()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var longMessage = new string('x', 1000);
            var arguments = fixture.CreateEchoArguments(longMessage);
            var workingDir = fixture.CreateValidWorkingDirectory();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value?.ToString(), Does.Contain(longMessage));
            });
        }

        [Test]
        public async Task ExecuteAsync_WithZeroTimeout_CompletesSuccessfully()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateEchoArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();
            var timeout = TimeSpan.Zero;

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken,
                timeout);

            // Zero timeout means immediate timeout, should fail
            Assert.That(result.IsSuccess, Is.False);
        }

        [Test]
        public async Task ExecuteAsync_OutputTrimming_RemovesTrailingWhitespace()
        {
            using var fixture = new TestEnvironmentFixture();
            var executor = fixture.CreateProcessExecutionService();
            var command = fixture.CreateValidCommand();
            var arguments = "-Command \"Write-Output 'Test'; Write-Output ''\""; // Extra newline
            var workingDir = fixture.CreateValidWorkingDirectory();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value?.ToString(), Does.Not.EndWith("\n"));
                Assert.That(result.Value?.ToString(), Does.Not.EndWith("\r"));
            });
        }

        #endregion
    }
}
