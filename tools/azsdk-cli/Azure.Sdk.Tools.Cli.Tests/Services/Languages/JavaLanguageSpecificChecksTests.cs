using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Services.Languages
{
    internal class JavaLanguageSpecificChecksTests
    {
        private string JavaPackageDir { get; set; }
        private Mock<IProcessHelper> MockProcessHelper { get; set; }
        private Mock<INpxHelper> MockNpxHelper { get; set; }
        private Mock<IGitHelper> MockGitHelper { get; set; }
        private JavaLanguageSpecificChecks LangService { get; set; }

        [SetUp]
        public void SetUp()
        {
            // Use TestAssets directory directly instead of temp directory
            JavaPackageDir = Path.Combine(
                Path.GetDirectoryName(typeof(JavaLanguageSpecificChecksTests).Assembly.Location)!,
                "TestAssets", "Java");

            MockProcessHelper = new Mock<IProcessHelper>();
            MockNpxHelper = new Mock<INpxHelper>();
            MockGitHelper = new Mock<IGitHelper>();

            LangService = new JavaLanguageSpecificChecks(
                MockProcessHelper.Object,
                MockNpxHelper.Object,
                MockGitHelper.Object,
                NullLogger<JavaLanguageSpecificChecks>.Instance);
        }
        
        [Test]
        public async Task TestFormatCodeAsync_MavenNotAvailable_ReturnsError()
        {
            // Arrange
            MockProcessHelper.Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "Maven not found")] });

            // Act
            var result = await LangService.FormatCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Maven is not installed or not available in PATH"));
            });
        }

        [Test]
        public async Task TestFormatCodeAsync_NoPomXml_ReturnsError()
        {
            // Arrange - Use a temp directory without pom.xml for this test
            var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(emptyDir);
            
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                ((p.Command == "mvn" || p.Command == "cmd.exe") || p.Command == "cmd.exe") && p.Args.Contains("--version")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Apache Maven 3.9.9")] });

            // Act
            var result = await LangService.FormatCodeAsync(emptyDir, false, CancellationToken.None);
            
            // Cleanup
            try { Directory.Delete(emptyDir, true); } catch { }

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("No pom.xml found"));
                Assert.That(result.ResponseError, Does.Contain("This doesn't appear to be a Maven project"));
            });
        }

        [Test]
        public async Task TestFormatCodeAsync_CheckMode_Success()
        {
            // Arrange
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("--version")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Apache Maven 3.9.9")] });

            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("spotless:check")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "BUILD SUCCESS")] });

            // Act
            var result = await LangService.FormatCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Is.EqualTo("Code formatting check passed - all files are properly formatted"));
            });
        }

        [Test]
        public async Task TestFormatCodeAsync_ApplyMode_Success()
        {
            // Arrange
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("--version")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Apache Maven 3.9.9")] });

            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("spotless:apply")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "BUILD SUCCESS")] });

            // Act
            var result = await LangService.FormatCodeAsync(JavaPackageDir, true, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Is.EqualTo("Code formatting applied successfully"));
            });
        }

        [Test]
        public async Task TestFormatCodeAsync_CheckMode_FormattingNeeded()
        {
            // Arrange
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("--version")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Apache Maven 3.9.9")] });

            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("spotless:check")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardOutput, "The following files had format violations")] });

            // Act
            var result = await LangService.FormatCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.CheckStatusDetails, Does.Contain("The following files had format violations"));
                Assert.That(result.ResponseError, Does.Contain("Code formatting check failed"));
                Assert.That(result.ResponseError, Does.Contain("mvn spotless:apply"));
            });
        }

        [Test]
        public async Task TestFormatCodeAsync_ApplyMode_Failure()
        {
            // Arrange
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("--version")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Apache Maven 3.9.9")] });

            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("spotless:apply")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardOutput, "spotless failed with errors")] });

            // Act
            var result = await LangService.FormatCodeAsync(JavaPackageDir, true, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.CheckStatusDetails, Does.Contain("spotless failed with errors"));
                Assert.That(result.ResponseError, Does.Contain("Code formatting failed to apply"));
            });
        }

        [Test]
        public async Task TestFormatCodeAsync_PomInParentDirectory()
        {
            // Arrange
            var subDir = Path.Combine(JavaPackageDir, "src", "main", "java");
            Directory.CreateDirectory(subDir);
            
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");

            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("--version")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Apache Maven 3.9.9")] });

            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("spotless:check") && p.Args.Contains("-f")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "BUILD SUCCESS")] });

            // Act - run from subdirectory
            var result = await LangService.FormatCodeAsync(subDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Is.EqualTo("Code formatting check passed - all files are properly formatted"));
            });

            // Verify the correct pom.xml path was used
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => 
                p.Args.Contains("-f") && 
                p.Args.Contains(pomPath)), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task TestFormatCodeAsync_ExceptionHandling()
        {
            // Arrange
            MockProcessHelper.Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Process execution failed"));

            // Act
            var result = await LangService.FormatCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Error formatting code: Process execution failed"));
            });
        }

        [Test]
        public async Task TestFormatCodeAsync_VerifyCorrectMavenCommand_CheckMode()
        {
            // Arrange
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");

            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("--version")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Apache Maven 3.9.9")] });

            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("spotless:check")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "BUILD SUCCESS")] });

            // Act
            await LangService.FormatCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert - verify the correct Maven command was called
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") &&
                p.Args.Contains("spotless:check") &&
                p.Args.Contains("-f") &&
                p.Args.Contains(pomPath) &&
                p.WorkingDirectory == JavaPackageDir &&
                p.Timeout == TimeSpan.FromMinutes(10)), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task TestFormatCodeAsync_VerifyCorrectMavenCommand_ApplyMode()
        {
            // Arrange
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");

            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("--version")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Apache Maven 3.9.9")] });

            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("spotless:apply")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "BUILD SUCCESS")] });

            // Act
            await LangService.FormatCodeAsync(JavaPackageDir, true, CancellationToken.None);

            // Assert - verify the correct Maven command was called
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") &&
                p.Args.Contains("spotless:apply") &&
                p.Args.Contains("-f") &&
                p.Args.Contains(pomPath) &&
                p.WorkingDirectory == JavaPackageDir &&
                p.Timeout == TimeSpan.FromMinutes(10)), 
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
