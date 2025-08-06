using Azure.Tools.GeneratorAgent;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.ComponentModel;
using System.Diagnostics;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class ProcessExecutorTests
    {
        private sealed class TestEnvironmentFixture : IDisposable
        {
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly string _uniqueId;

            public TestEnvironmentFixture()
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _uniqueId = Guid.NewGuid().ToString("N")[..8];
            }
            
            public CancellationToken CancellationToken => _cancellationTokenSource.Token;

            public Mock<ILogger<ProcessExecutor>> CreateMockLogger()
            {
                return new Mock<ILogger<ProcessExecutor>>();
            }

            public ProcessExecutor CreateProcessExecutor(Mock<ILogger<ProcessExecutor>>? mockLogger = null)
            {
                return new ProcessExecutor((mockLogger ?? CreateMockLogger()).Object);
            }

            public string CreateValidCommand() => "pwsh";

            public string CreateEchoArguments(string message = "test message") 
                => $"-Command \"Write-Host '{message}'\"";

            public string CreateFailArguments(int exitCode = 1) 
                => $"-Command \"exit {exitCode}\"";

            public string CreateValidWorkingDirectory() 
                => Environment.GetFolderPath(Environment.SpecialFolder.System);

            public string CreateExpectedOutput(string message = "test message") 
                => message;

            public TimeSpan CreateDefaultTimeout() 
                => TimeSpan.FromMilliseconds(5000);

            public string CreateMultilineArguments(params string[] lines)
            {
                var defaultLines = lines.Length == 0 ? new[] { "Line1", "Line2", "Line3" } : lines;
                var commands = string.Join("; ", defaultLines.Select(line => $"Write-Host '{line}'"));
                return $"-Command \"{commands}\"";
            }

            public string CreateLongRunningArguments(int sleepSeconds = 10)
                => $"-Command \"Start-Sleep {sleepSeconds}\"";

            public string CreateNonExistentCommand()
                => $"nonexistent-command-{_uniqueId}";

            public string CreateInvalidWorkingDirectory()
                => Path.Combine(Path.GetTempPath(), $"NonExistentDirectory{_uniqueId}");

            public TimeSpan CreateShortTimeout(int milliseconds = 100)
                => TimeSpan.FromMilliseconds(milliseconds);

            public TimeSpan CreateCustomTimeout(int milliseconds)
                => TimeSpan.FromMilliseconds(milliseconds);

            public void CancelAfterDelay(int milliseconds)
            {
                _cancellationTokenSource.CancelAfter(milliseconds);
            }

            public void Dispose()
            {
                _cancellationTokenSource?.Dispose();
            }
        }

        [Test]
        public async Task ExecuteAsync_WithValidCommand_ReturnsSuccessResult()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var executor = fixture.CreateProcessExecutor();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateEchoArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();
            var expectedOutput = fixture.CreateExpectedOutput();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Output.Trim(), Is.EqualTo(expectedOutput));
                Assert.That(result.Error, Is.Empty);
            });
        }

        [Test]
        public async Task ExecuteAsync_WithFailingCommand_ReturnsFailureResult()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var executor = fixture.CreateProcessExecutor();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateFailArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Output, Is.Empty);
                Assert.That(result.Error, Is.Empty);
            });
        }

        [Test]
        public void ExecuteAsync_WithNullCommand_ThrowsArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var executor = fixture.CreateProcessExecutor();
            var arguments = fixture.CreateEchoArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var exception = Assert.ThrowsAsync<ArgumentException>(async () =>
                await executor.ExecuteAsync(
                    null!,
                    arguments,
                    workingDir,
                    fixture.CancellationToken));

            Assert.That(exception!.ParamName, Is.EqualTo("command"));
        }

        [Test]
        public void ExecuteAsync_WithEmptyCommand_ThrowsArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var executor = fixture.CreateProcessExecutor();
            var arguments = fixture.CreateEchoArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var exception = Assert.ThrowsAsync<ArgumentException>(async () =>
                await executor.ExecuteAsync(
                    string.Empty,
                    arguments,
                    workingDir,
                    fixture.CancellationToken));

            Assert.That(exception!.ParamName, Is.EqualTo("command"));
        }

        [Test]
        public void ExecuteAsync_WithWhitespaceCommand_ThrowsArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var executor = fixture.CreateProcessExecutor();
            var arguments = fixture.CreateEchoArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var exception = Assert.ThrowsAsync<ArgumentException>(async () =>
                await executor.ExecuteAsync(
                    "   ",
                    arguments,
                    workingDir,
                    fixture.CancellationToken));

            Assert.That(exception!.ParamName, Is.EqualTo("command"));
        }

        [Test]
        public async Task ExecuteAsync_WithNullArguments_UsesEmptyArguments()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var executor = fixture.CreateProcessExecutor();
            var command = fixture.CreateValidCommand();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var result = await executor.ExecuteAsync(
                command,
                null!,
                workingDir,
                fixture.CancellationToken);

            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task ExecuteAsync_WithTimeout_CompletesWithinTimeout()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var executor = fixture.CreateProcessExecutor();
            var timeout = fixture.CreateDefaultTimeout();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateEchoArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken,
                timeout);

            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task ExecuteAsync_WithTimeoutExceeded_ReturnsTimeoutError()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var executor = fixture.CreateProcessExecutor();
            var longRunningArgs = fixture.CreateLongRunningArguments();
            var timeout = fixture.CreateShortTimeout();
            var command = fixture.CreateValidCommand();
            var workingDir = fixture.CreateValidWorkingDirectory();
            
            var result = await executor.ExecuteAsync(
                command,
                longRunningArgs,
                workingDir,
                fixture.CancellationToken,
                timeout);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Does.Contain("timed out"));
            });
        }

        [Test]
        public async Task ExecuteAsync_WithCancellation_ReturnsCancellationError()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var executor = fixture.CreateProcessExecutor();
            var longRunningArgs = fixture.CreateLongRunningArguments();
            var command = fixture.CreateValidCommand();
            var workingDir = fixture.CreateValidWorkingDirectory();
            fixture.CancelAfterDelay(100);

            var result = await executor.ExecuteAsync(
                command,
                longRunningArgs,
                workingDir,
                fixture.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error, Is.EqualTo("Operation was cancelled"));
            });
        }

        [Test]
        public void ExecuteAsync_WithNonexistentCommand_ReturnsCommandNotFoundError()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var executor = fixture.CreateProcessExecutor();
            var nonExistentCommand = fixture.CreateNonExistentCommand();
            var arguments = fixture.CreateEchoArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await executor.ExecuteAsync(
                    nonExistentCommand,
                    arguments,
                    workingDir,
                    fixture.CancellationToken);
            });
        }

        [Test]
        public async Task ExecuteAsync_LogsSuccessfulCommand()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var executor = fixture.CreateProcessExecutor(mockLogger);
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateEchoArguments();
            var workingDir = fixture.CreateValidWorkingDirectory();

            await executor.ExecuteAsync(
                command,
                arguments,
                workingDir,
                fixture.CancellationToken);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Command succeeded")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_LogsFailedCommand()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var executor = fixture.CreateProcessExecutor(mockLogger);
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateFailArguments();
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
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Command failed with exit code")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_LogsCancelledCommand()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var mockLogger = fixture.CreateMockLogger();
            var executor = fixture.CreateProcessExecutor(mockLogger);
            var longRunningArgs = "/c ping 127.0.0.1 -n 10";
            var command = fixture.CreateValidCommand();
            var workingDir = fixture.CreateValidWorkingDirectory();
            fixture.CancelAfterDelay(100);

            await executor.ExecuteAsync(
                command,
                longRunningArgs,
                workingDir,
                fixture.CancellationToken);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Command execution was cancelled")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_WithMultilineOutput_CapturesAllLines()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var executor = fixture.CreateProcessExecutor();
            var multilineArgs = fixture.CreateMultilineArguments();
            var command = fixture.CreateValidCommand();
            var workingDir = fixture.CreateValidWorkingDirectory();
            
            var result = await executor.ExecuteAsync(
                command,
                multilineArgs,
                workingDir,
                fixture.CancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Output, Does.Contain("Line1"));
                Assert.That(result.Output, Does.Contain("Line2"));
                Assert.That(result.Output, Does.Contain("Line3"));
            });
        }

        [Test]
        public void ExecuteAsync_WithInvalidWorkingDirectory_HandlesGracefully()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var executor = fixture.CreateProcessExecutor();
            var invalidDir = fixture.CreateInvalidWorkingDirectory();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateEchoArguments();
            
            Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await executor.ExecuteAsync(
                    command,
                    arguments,
                    invalidDir,
                    fixture.CancellationToken);
            });
        }

        [Test]
        public async Task ExecuteAsync_WithNullWorkingDirectory_UsesCurrentDirectory()
        {
            using var fixture = new TestEnvironmentFixture();

            var executor = fixture.CreateProcessExecutor();
            var command = fixture.CreateValidCommand();
            var arguments = fixture.CreateEchoArguments();

            var result = await executor.ExecuteAsync(
                command,
                arguments,
                null!,
                fixture.CancellationToken);

            Assert.That(result.Success, Is.True);
        }
    }
}
