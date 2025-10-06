// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Runtime.InteropServices;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    [TestFixture]
    public class DockerServiceTests
    {
        private Mock<IProcessHelper> _mockProcessHelper;
        private DockerService _dockerService;
        private static readonly string DockerCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "docker.exe" : "docker";

        [SetUp]
        public void SetUp()
        {
            _mockProcessHelper = new Mock<IProcessHelper>();
            _dockerService = new DockerService(_mockProcessHelper.Object, NullLogger<DockerService>.Instance);
        }

        [Test]
        public void Constructor_WithNullProcessHelper_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DockerService(null!, NullLogger<DockerService>.Instance));
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DockerService(_mockProcessHelper.Object, null!));
        }

        [Test]
        public async Task IsDockerAvailableAsync_WhenDockerIsAvailable_ReturnsTrue()
        {
            // Arrange
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.SequenceEqual(new[] { "--version" }) &&
                    !opt.LogOutputStream), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.IsDockerAvailableAsync(CancellationToken.None);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task IsDockerAvailableAsync_WhenDockerIsNotAvailable_ReturnsFalse()
        {
            // Arrange
            var expectedResult = new ProcessResult { ExitCode = 1 };
            _mockProcessHelper
                .Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.IsDockerAvailableAsync(CancellationToken.None);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task IsDockerAvailableAsync_WhenExceptionThrown_ReturnsFalse()
        {
            // Arrange
            _mockProcessHelper
                .Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Docker not found"));

            // Act
            var result = await _dockerService.IsDockerAvailableAsync(CancellationToken.None);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task PullImageAsync_WithValidImage_CallsDockerPull()
        {
            // Arrange
            const string imageName = "ubuntu:latest";
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.SequenceEqual(new[] { "pull", imageName })), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.PullImageAsync(imageName, CancellationToken.None);

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));
            _mockProcessHelper.Verify(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void PullImageAsync_WithNullImage_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.PullImageAsync(null!, CancellationToken.None));
        }

        [Test]
        public void PullImageAsync_WithEmptyImage_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.PullImageAsync("", CancellationToken.None));
        }

        [Test]
        public async Task CreateContainerAsync_WithImageOnly_CallsDockerCreate()
        {
            // Arrange
            const string imageName = "ubuntu:latest";
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.Contains("create") &&
                    opt.Args.Contains(imageName)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.CreateContainerAsync(imageName, ct: CancellationToken.None);

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public async Task CreateContainerAsync_WithContainerName_IncludesNameInArgs()
        {
            // Arrange
            const string imageName = "ubuntu:latest";
            const string containerName = "test-container";
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.Contains("--name") &&
                    opt.Args.Contains(containerName)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.CreateContainerAsync(imageName, containerName, ct: CancellationToken.None);

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public async Task CreateContainerAsync_WithEnvironmentVars_IncludesEnvInArgs()
        {
            // Arrange
            const string imageName = "ubuntu:latest";
            var envVars = new Dictionary<string, string> { { "TEST_VAR", "test_value" } };
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.Contains("-e") &&
                    opt.Args.Contains("TEST_VAR=test_value")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.CreateContainerAsync(imageName, environmentVars: envVars, ct: CancellationToken.None);

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public async Task CreateContainerAsync_WithWorkingDirectory_IncludesWorkdirInArgs()
        {
            // Arrange
            const string imageName = "ubuntu:latest";
            const string workingDir = "/app";
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.Contains("-w") &&
                    opt.Args.Contains(workingDir)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.CreateContainerAsync(imageName, workingDirectory: workingDir, ct: CancellationToken.None);

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public void CreateContainerAsync_WithNullImage_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.CreateContainerAsync(null!, ct: CancellationToken.None));
        }

        [Test]
        public async Task IsContainerRunningAsync_WhenContainerIsRunning_ReturnsTrue()
        {
            // Arrange
            const string containerName = "test-container";
            var expectedResult = new ProcessResult { ExitCode = 0 };
            expectedResult.AppendStdout(containerName);
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.Contains("ps") &&
                    opt.Args.Contains($"name={containerName}")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.IsContainerRunningAsync(containerName, CancellationToken.None);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task IsContainerRunningAsync_WhenContainerIsNotRunning_ReturnsFalse()
        {
            // Arrange
            const string containerName = "test-container";
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.IsContainerRunningAsync(containerName, CancellationToken.None);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsContainerRunningAsync_WithNullContainerName_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.IsContainerRunningAsync(null!, CancellationToken.None));
        }

        [Test]
        public async Task StartContainerAsync_WithValidContainer_CallsDockerStart()
        {
            // Arrange
            const string containerName = "test-container";
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.SequenceEqual(new[] { "start", containerName })), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.StartContainerAsync(containerName, CancellationToken.None);

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public void StartContainerAsync_WithNullContainerName_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.StartContainerAsync(null!, CancellationToken.None));
        }

        [Test]
        public async Task StopContainerAsync_WithValidContainer_CallsDockerStop()
        {
            // Arrange
            const string containerName = "test-container";
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.SequenceEqual(new[] { "stop", containerName })), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.StopContainerAsync(containerName, CancellationToken.None);

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public void StopContainerAsync_WithNullContainerName_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.StopContainerAsync(null!, CancellationToken.None));
        }

        [Test]
        public async Task RunCommandInContainerAsync_WithValidParams_CallsDockerExec()
        {
            // Arrange
            const string containerName = "test-container";
            var command = new[] { "echo", "hello" };
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.Contains("exec") &&
                    opt.Args.Contains(containerName) &&
                    opt.Args.Contains("echo") &&
                    opt.Args.Contains("hello")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.RunCommandInContainerAsync(containerName, command, ct: CancellationToken.None);

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public async Task RunCommandInContainerAsync_WithWorkingDirectory_IncludesWorkdirInArgs()
        {
            // Arrange
            const string containerName = "test-container";
            const string workingDir = "/app";
            var command = new[] { "ls" };
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.Contains("-w") &&
                    opt.Args.Contains(workingDir)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.RunCommandInContainerAsync(containerName, command, workingDir, CancellationToken.None);

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public void RunCommandInContainerAsync_WithNullContainerName_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.RunCommandInContainerAsync(null!, new[] { "echo" }, ct: CancellationToken.None));
        }

        [Test]
        public void RunCommandInContainerAsync_WithNullCommand_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.RunCommandInContainerAsync("container", null!, ct: CancellationToken.None));
        }

        [Test]
        public void RunCommandInContainerAsync_WithEmptyCommand_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.RunCommandInContainerAsync("container", Array.Empty<string>(), ct: CancellationToken.None));
        }

        [Test]
        public async Task CopyToContainerAsync_WithValidParams_CallsDockerCp()
        {
            // Arrange
            const string containerName = "test-container";
            const string hostPath = "/host/file.txt";
            const string containerPath = "/container/file.txt";
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.SequenceEqual(new[] { "cp", hostPath, $"{containerName}:{containerPath}" })), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.CopyToContainerAsync(containerName, hostPath, containerPath, CancellationToken.None);

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public void CopyToContainerAsync_WithNullContainerName_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.CopyToContainerAsync(null!, "/host/path", "/container/path", CancellationToken.None));
        }

        [Test]
        public void CopyToContainerAsync_WithNullHostPath_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.CopyToContainerAsync("container", null!, "/container/path", CancellationToken.None));
        }

        [Test]
        public void CopyToContainerAsync_WithNullContainerPath_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.CopyToContainerAsync("container", "/host/path", null!, CancellationToken.None));
        }

        [Test]
        public async Task CopyFromContainerAsync_WithValidParams_CallsDockerCp()
        {
            // Arrange
            const string containerName = "test-container";
            const string containerPath = "/container/file.txt";
            const string hostPath = "/host/file.txt";
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.SequenceEqual(new[] { "cp", $"{containerName}:{containerPath}", hostPath })), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.CopyFromContainerAsync(containerName, containerPath, hostPath, CancellationToken.None);

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public void CopyFromContainerAsync_WithNullContainerName_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.CopyFromContainerAsync(null!, "/container/path", "/host/path", CancellationToken.None));
        }

        [Test]
        public void CopyFromContainerAsync_WithNullContainerPath_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.CopyFromContainerAsync("container", null!, "/host/path", CancellationToken.None));
        }

        [Test]
        public void CopyFromContainerAsync_WithNullHostPath_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.CopyFromContainerAsync("container", "/container/path", null!, CancellationToken.None));
        }

        [Test]
        public async Task RemoveContainerAsync_WithoutForce_CallsDockerRm()
        {
            // Arrange
            const string containerName = "test-container";
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.SequenceEqual(new[] { "rm", containerName })), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.RemoveContainerAsync(containerName, ct: CancellationToken.None);

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public async Task RemoveContainerAsync_WithForce_CallsDockerRmWithForceFlag()
        {
            // Arrange
            const string containerName = "test-container";
            var expectedResult = new ProcessResult { ExitCode = 0 };
            _mockProcessHelper
                .Setup(x => x.Run(It.Is<ProcessOptions>(opt => 
                    opt.Command == DockerCommand && 
                    opt.Args.SequenceEqual(new[] { "rm", "-f", containerName })), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _dockerService.RemoveContainerAsync(containerName, force: true, ct: CancellationToken.None);

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public void RemoveContainerAsync_WithNullContainerName_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(() => _dockerService.RemoveContainerAsync(null!, ct: CancellationToken.None));
        }
    }
}