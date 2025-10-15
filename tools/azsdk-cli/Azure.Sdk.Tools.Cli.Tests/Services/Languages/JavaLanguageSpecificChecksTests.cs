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
        private JavaLanguageSpecificChecks LangService { get; set; }

        [SetUp]
        public void SetUp()
        {
            // Use TestAssets directory directly instead of temp directory
            JavaPackageDir = Path.Combine(
                Path.GetDirectoryName(typeof(JavaLanguageSpecificChecksTests).Assembly.Location)!,
                "TestAssets", "Java");

            MockProcessHelper = new Mock<IProcessHelper>();

            LangService = new JavaLanguageSpecificChecks(
                MockProcessHelper.Object,
                NullLogger<JavaLanguageSpecificChecks>.Instance);
        }

        #region Setup Helpers

        /// <summary>
        /// Sets up successful Maven version check for both Unix and Windows platforms.
        /// </summary>
        private void SetupSuccessfulMavenVersionCheck()
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" && p.Args.Contains("--version")) || 
                (p.Command == "cmd.exe" && p.Args.Contains("mvn") && p.Args.Contains("--version"))), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Apache Maven 3.9.9")] });
        }

        /// <summary>
        /// Sets up failed Maven version check for both Unix and Windows platforms.
        /// </summary>
        private void SetupFailedMavenVersionCheck()
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" && p.Args.Contains("--version")) || 
                (p.Command == "cmd.exe" && p.Args.Contains("mvn") && p.Args.Contains("--version"))), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "Maven not found")] });
        }

        /// <summary>
        /// Sets up successful Maven spotless check command.
        /// </summary>
        private void SetupSuccessfulSpotlessCheck()
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("spotless:check")), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "BUILD SUCCESS")] });
        }

        /// <summary>
        /// Sets up successful Maven spotless apply command.
        /// </summary>
        private void SetupSuccessfulSpotlessApply()
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("spotless:apply")), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "BUILD SUCCESS")] });
        }

        /// <summary>
        /// Sets up failed Maven spotless check command with formatting violations.
        /// </summary>
        private void SetupFailedSpotlessCheck()
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("spotless:check")), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardOutput, "The following files had format violations")] });
        }

        /// <summary>
        /// Sets up failed Maven spotless apply command.
        /// </summary>
        private void SetupFailedSpotlessApply()
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("spotless:apply")), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardOutput, "spotless failed with errors")] });
        }

        /// <summary>
        /// Sets up successful Maven install command (Azure SDK approach).
        /// </summary>
        private void SetupSuccessfulMavenInstall()
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("install")), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { 
                    ExitCode = 0, 
                    OutputDetails = [(StdioLevel.StandardOutput, 
                        "[INFO] BUILD SUCCESS\n" +
                        "[INFO] Total time: 30.123 s\n" +
                        "[INFO] Finished at: 2025-01-09T10:30:00-08:00"
                    )] 
                });
        }

        /// <summary>
        /// Sets up Maven install command that fails with specific tool errors.
        /// </summary>
        private void SetupMavenInstallWithToolErrors(string errorOutput)
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") && p.Args.Contains("install")), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, errorOutput)] });
        }

         /// <summary>
        /// Sets up successful Maven codesnippet update command.
        /// </summary>
        private void SetupSuccessfulSnippetUpdate()
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") && 
                p.Args.Any(arg => arg.Contains("codesnippet-maven-plugin"))), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "BUILD SUCCESS")] });
        }

        /// <summary>
        /// Sets up failed Maven codesnippet update command.
        /// </summary>
        private void SetupFailedSnippetUpdate()
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") && 
                p.Args.Any(arg => arg.Contains("codesnippet-maven-plugin"))), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "Codesnippet update failed")] });
        }

        /// <summary>
        /// Sets up successful Maven javadoc command.
        /// </summary>
        private void SetupSuccessfulJavadoc()
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") && 
                p.Args.Contains("javadoc:jar")), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { 
                    ExitCode = 0, 
                    OutputDetails = [(StdioLevel.StandardOutput, 
                        "[INFO] Building jar: /path/to/target/test-1.0-javadoc.jar\n" +
                        "[INFO] BUILD SUCCESS"
                    )] 
                });
        }

        /// <summary>
        /// Sets up failed Maven javadoc command.
        /// </summary>
        private void SetupFailedJavadoc()
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") && 
                p.Args.Contains("javadoc:jar")), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { 
                    ExitCode = 1, 
                    OutputDetails = [(StdioLevel.StandardError, 
                        "[ERROR] Failed to execute goal org.apache.maven.plugins:maven-javadoc-plugin:3.4.1:jar (default) on project test: MavenReportException: Error while generating Javadoc:\n" +
                        "[ERROR] Exit code: 1 - javadoc: error - The code being documented uses modules but the packages defined in https://docs.oracle.com/javase/8/docs/api/ are in the unnamed module.\n" +
                        "[ERROR] Command line was: /usr/lib/jvm/java-11-openjdk/bin/javadoc @options @packages"
                    )] 
                });
        }

        #endregion
        
        [Test]
        public async Task TestFormatCodeAsync_MavenNotAvailable_ReturnsError()
        {
            // Arrange
            SetupFailedMavenVersionCheck();

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
            
            SetupSuccessfulMavenVersionCheck();

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
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulSpotlessCheck();

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
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulSpotlessApply();

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
            SetupSuccessfulMavenVersionCheck();
            SetupFailedSpotlessCheck();

            // Act
            var result = await LangService.FormatCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.CheckStatusDetails, Does.Contain("The following files had format violations"));
                Assert.That(result.ResponseError, Does.Contain("Code formatting check failed"));
                Assert.That(result.NextSteps, Is.Not.Null);
                Assert.That(result.NextSteps, Has.Count.EqualTo(1));
                Assert.That(result.NextSteps![0], Does.Contain("mvn spotless:apply"));
            });
        }

        [Test]
        public async Task TestFormatCodeAsync_ApplyMode_Failure()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupFailedSpotlessApply();

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
        public async Task TestFormatCodeAsync_NoPomInPackageDirectory_ReturnsError()
        {
            // Arrange
            var subDir = Path.Combine(JavaPackageDir, "src", "main", "java");
            Directory.CreateDirectory(subDir);
            
            // Create pom.xml in parent directory but not in the package directory we're testing
            var parentPomPath = Path.Combine(JavaPackageDir, "pom.xml");
            File.WriteAllText(parentPomPath, "<project></project>");

            SetupSuccessfulMavenVersionCheck();

            // Act - run from subdirectory where there's no pom.xml
            var result = await LangService.FormatCodeAsync(subDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.CheckStatusDetails, Is.Empty);
                Assert.That(result.ResponseError, Does.Contain("No pom.xml found"));
                Assert.That(result.ResponseError, Does.Contain("This doesn't appear to be a Maven project"));
                Assert.That(result.NextSteps, Has.Count.GreaterThan(0));
            });
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
                Assert.That(result.ResponseError, Does.Contain("Error during code formatting: Process execution failed"));
            });
        }

        [Test]
        public async Task TestFormatCodeAsync_VerifyCorrectMavenCommand_CheckMode()
        {
            // Arrange
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulSpotlessCheck();

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
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulSpotlessApply();

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

        #region LintCodeAsync Tests

        [Test]
        public async Task TestLintCodeAsync_MavenNotInstalled_ReturnsError()
        {
            // Arrange
            SetupFailedMavenVersionCheck();

            // Act
            var result = await LangService.LintCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Maven is not installed or not available in PATH"));
            });
        }

        [Test]
        public async Task TestLintCodeAsync_NoPomXml_ReturnsError()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            
            var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(emptyDir);

            try
            {
                // Act
                var result = await LangService.LintCodeAsync(emptyDir, false, CancellationToken.None);

                // Assert
                Assert.Multiple(() =>
                {
                    Assert.That(result.ExitCode, Is.EqualTo(1));
                    Assert.That(result.ResponseError, Does.Contain("No pom.xml found"));
                });
            }
            finally
            {
                try { Directory.Delete(emptyDir, true); } catch { }
            }
        }

        [Test]
        public async Task TestLintCodeAsync_AllToolsPass_ReturnsSuccess()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulMavenInstall();
            SetupSuccessfulJavadoc();

            // Act
            var result = await LangService.LintCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Does.Contain("Code linting passed"));
                Assert.That(result.CheckStatusDetails, Does.Contain("All tools successful"));
                Assert.That(result.CheckStatusDetails, Does.Contain("Checkstyle, SpotBugs, RevAPI, Javadoc"));
            });

            // Verify Maven commands were called correctly
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenVersionCheck(p)), It.IsAny<CancellationToken>()), Times.Once);
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenInstallCommand(p)), It.IsAny<CancellationToken>()), Times.Once);
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsJavadocCommand(p)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task TestLintCodeAsync_CheckstyleFails_ReturnsError()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            var checkstyleErrorOutput = "[ERROR] Failed to execute goal com.puppycrawl.tools:checkstyle:9.3:check (default) on project test: You have 5 Checkstyle violations.";
            SetupMavenInstallWithToolErrors(checkstyleErrorOutput);
            SetupSuccessfulJavadoc(); // Javadoc succeeds, but Checkstyle fails

            // Act
            var result = await LangService.LintCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert - Azure SDK approach: Maven install failed with Checkstyle errors, but javadoc passed
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Code linting found issues"));
                Assert.That(result.ResponseError, Does.Contain("Checkstyle"));
                Assert.That(result.ResponseError, Does.Contain("Clean tools: SpotBugs, RevAPI, Javadoc"));
                Assert.That(result.CheckStatusDetails, Does.Contain("checkstyle"));
                Assert.That(result.NextSteps, Is.Not.Null);
                Assert.That(result.NextSteps, Has.Count.GreaterThan(0));
                Assert.That(result.NextSteps!.Any(step => step.Contains("Checkstyle")), Is.True);
            });

            // Verify Maven commands were called correctly
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenVersionCheck(p)), It.IsAny<CancellationToken>()), Times.Once);
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenInstallCommand(p)), It.IsAny<CancellationToken>()), Times.Once);
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsJavadocCommand(p)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task TestLintCodeAsync_AllToolsFail_ReturnsError()
        {
            // Arrange - Reset mock to avoid interference from other test setups
            MockProcessHelper.Reset();
            
            var capturedOptions = new List<ProcessOptions>();
            MockProcessHelper.Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions.Add(options))
                .ReturnsAsync((ProcessOptions options, CancellationToken _) =>
                {
                    if (IsMavenVersionCheck(options))
                    {
                        return new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Apache Maven 3.9.9")] };
                    }
                    if (IsMavenInstallCommand(options))
                    {
                        var errorOutput = "[ERROR] Failed to execute goal com.puppycrawl.tools:checkstyle:9.3:check (default) on project test: You have 5 Checkstyle violations.\n" +
                                        "[ERROR] Failed to execute goal com.github.spotbugs:spotbugs-maven-plugin:4.8.2.0:check (default) on project test: BugInstance size is 2\n" +
                                        "[ERROR] Failed to execute goal org.revapi:revapi-maven-plugin:0.15.1:check (default) on project test: API problems detected";
                        return new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, errorOutput)] };
                    }
                    if (IsJavadocCommand(options))
                    {
                        var javadocError = "[ERROR] Failed to execute goal org.apache.maven.plugins:maven-javadoc-plugin:3.4.1:jar (default) on project test: MavenReportException: Error while generating Javadoc";
                        return new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, javadocError)] };
                    }
                    
                    return new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "Unknown command")] };
                });

            // Act
            var result = await LangService.LintCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert - Azure SDK approach: All tools failed (Maven install command + javadoc)
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Code linting found issues"));
                Assert.That(result.ResponseError, Does.Contain("Checkstyle, SpotBugs, RevAPI, Javadoc"));
                Assert.That(result.CheckStatusDetails, Does.Contain("checkstyle"));
                Assert.That(result.CheckStatusDetails, Does.Contain("spotbugs-maven-plugin"));
                Assert.That(result.CheckStatusDetails, Does.Contain("revapi-maven-plugin"));
                Assert.That(result.CheckStatusDetails, Does.Contain("--- Javadoc Validation ---"));
                Assert.That(result.CheckStatusDetails, Does.Contain("maven-javadoc-plugin"));
                Assert.That(result.NextSteps, Is.Not.Null);
                Assert.That(result.NextSteps, Has.Count.GreaterThan(0));
                Assert.That(result.NextSteps!.Any(step => step.Contains("Checkstyle")), Is.True);
                Assert.That(result.NextSteps!.Any(step => step.Contains("SpotBugs")), Is.True);
                Assert.That(result.NextSteps!.Any(step => step.Contains("RevAPI")), Is.True);
                Assert.That(result.NextSteps!.Any(step => step.Contains("Javadoc")), Is.True);
            });

            // Verify captured commands - Azure SDK approach uses install + javadoc commands
            Assert.That(capturedOptions, Has.Count.EqualTo(3)); // Maven version + install + javadoc commands
            Assert.That(capturedOptions.Any(IsMavenVersionCheck), Is.True, "Maven version check should be called");
            Assert.That(capturedOptions.Any(IsMavenInstallCommand), Is.True, "Maven install should be called");
            Assert.That(capturedOptions.Any(IsJavadocCommand), Is.True, "Javadoc command should be called");
        }

        [Test]
        public async Task TestLintCodeAsync_WithFix_UsesFailOnViolationFalse()
        {
            // Arrange - Reset mock to avoid interference from other test setups
            MockProcessHelper.Reset();
            
            var capturedOptions = new List<ProcessOptions>();
            MockProcessHelper.Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                .Callback<ProcessOptions, CancellationToken>((options, _) => capturedOptions.Add(options))
                .ReturnsAsync((ProcessOptions options, CancellationToken _) =>
                {
                    if (IsMavenVersionCheck(options))
                    {
                        return new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Apache Maven 3.9.9")] };
                    }
                    if (IsMavenInstallCommand(options))
                    {
                        // Verify it has the correct RevAPI property for fix mode
                        var argsString = string.Join(" ", options.Args);
                        if (argsString.Contains("-Drevapi.failBuildOnProblemsFound=false"))
                        {
                            return new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "BUILD SUCCESS")] };
                        }
                        return new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "Missing fix parameter")] };
                    }
                    if (IsJavadocCommand(options))
                    {
                        return new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "[INFO] Building jar: /path/to/target/test-1.0-javadoc.jar")] };
                    }
                    
                    return new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "Unknown command")] };
                });

            // Act
            await LangService.LintCodeAsync(JavaPackageDir, true, CancellationToken.None);

            // Assert - verify the Maven install command was called with fix parameters
            Assert.That(capturedOptions, Has.Count.EqualTo(3)); // Maven version + install + javadoc commands
            Assert.That(capturedOptions.Any(IsMavenVersionCheck), Is.True, "Maven version check should be called");
            Assert.That(capturedOptions.Any(IsMavenInstallCommand), Is.True, "Maven install should be called");
            Assert.That(capturedOptions.Any(IsJavadocCommand), Is.True, "Javadoc command should be called");
            
            // Verify the install command includes the correct RevAPI fix parameter
            var installCommand = capturedOptions.FirstOrDefault(IsMavenInstallCommand);
            Assert.That(installCommand, Is.Not.Null);
            var argsString = string.Join(" ", installCommand.Args);
            Assert.That(argsString, Does.Contain("-Drevapi.failBuildOnProblemsFound=false"));
            
            // Verify pom.xml path is included in Maven install command
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            Assert.That(installCommand.Args.Contains("-f"), Is.True, "Maven install command should include -f flag");
            Assert.That(installCommand.Args.Contains(pomPath), Is.True, "Maven install command should include pom.xml path");
        }

        [Test]
        public async Task TestLintCodeAsync_Exception_ReturnsError()
        {
            // Arrange
            MockProcessHelper.Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Process execution failed"));

            // Act
            var result = await LangService.LintCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Error during code linting: Process execution failed"));
            });
        }

        [Test]
        public async Task TestLintCodeAsync_VerifyCorrectMavenCommands()
        {
            // Arrange
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulMavenInstall();

            // Act
            await LangService.LintCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert - verify Azure SDK style Maven install command was called correctly
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") &&
                p.Args.Contains("install") &&
                p.Args.Contains("--no-transfer-progress") &&
                p.Args.Contains("-DskipTests") &&
                p.Args.Contains("-Dgpg.skip") &&
                p.Args.Contains("-Dmaven.javadoc.skip=true") &&
                p.Args.Contains("-Dcodesnippet.skip=true") &&
                p.Args.Contains("-Dspotless.apply.skip=true") &&
                p.Args.Contains("-am") &&
                p.Args.Contains("-f") &&
                p.Args.Contains(pomPath) &&
                p.WorkingDirectory == JavaPackageDir &&
                p.Timeout == TimeSpan.FromMinutes(15)), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task TestLintCodeAsync_JavadocFails_ReturnsError()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulMavenInstall();
            SetupFailedJavadoc();

            // Act
            var result = await LangService.LintCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Code linting found issues"));
                Assert.That(result.ResponseError, Does.Contain("Javadoc"));
                Assert.That(result.CheckStatusDetails, Does.Contain("--- Javadoc Validation ---"));
                Assert.That(result.CheckStatusDetails, Does.Contain("maven-javadoc-plugin"));
                Assert.That(result.NextSteps, Is.Not.Null);
                Assert.That(result.NextSteps!.Any(step => step.Contains("Javadoc")), Is.True);
                Assert.That(result.NextSteps!.Any(step => step.Contains("javadoc comments")), Is.True);
            });

            // Verify Maven commands were called correctly
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenVersionCheck(p)), It.IsAny<CancellationToken>()), Times.Once);
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenInstallCommand(p)), It.IsAny<CancellationToken>()), Times.Once);
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsJavadocCommand(p)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task TestLintCodeAsync_CheckstyleAndJavadocFail_ReturnsError()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            var checkstyleErrorOutput = "[ERROR] Failed to execute goal com.puppycrawl.tools:checkstyle:9.3:check (default) on project test: You have 5 Checkstyle violations.";
            SetupMavenInstallWithToolErrors(checkstyleErrorOutput);
            SetupFailedJavadoc();

            // Act
            var result = await LangService.LintCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert - Both Checkstyle and Javadoc should report issues
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Code linting found issues"));
                Assert.That(result.ResponseError, Does.Contain("Checkstyle, Javadoc"));
                Assert.That(result.CheckStatusDetails, Does.Contain("checkstyle"));
                Assert.That(result.CheckStatusDetails, Does.Contain("--- Javadoc Validation ---"));
                Assert.That(result.CheckStatusDetails, Does.Contain("maven-javadoc-plugin"));
                Assert.That(result.NextSteps, Is.Not.Null);
                Assert.That(result.NextSteps!.Any(step => step.Contains("Checkstyle")), Is.True);
                Assert.That(result.NextSteps!.Any(step => step.Contains("Javadoc")), Is.True);
                Assert.That(result.NextSteps!.Any(step => step.Contains("-Dmaven.javadoc.skip=true")), Is.True);
            });

            // Verify Maven commands were called correctly
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenVersionCheck(p)), It.IsAny<CancellationToken>()), Times.Once);
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenInstallCommand(p)), It.IsAny<CancellationToken>()), Times.Once);
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsJavadocCommand(p)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task TestLintCodeAsync_JavadocCommandParameters_AreCorrect()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulMavenInstall();
            SetupSuccessfulJavadoc();

            // Act
            await LangService.LintCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert - verify the javadoc command was called with correct parameters
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") &&
                p.Args.Contains("--no-transfer-progress") &&
                p.Args.Contains("javadoc:jar") &&
                p.Args.Contains("-f") &&
                p.Args.Contains(pomPath) &&
                p.WorkingDirectory == JavaPackageDir &&
                p.Timeout == TimeSpan.FromMinutes(15)), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #region UpdateSnippetsAsync Tests

        [Test]
        public async Task TestUpdateSnippetsAsync_MavenNotAvailable_ReturnsError()
        {
            // Arrange
            SetupFailedMavenVersionCheck();

            // Act
            var result = await LangService.UpdateSnippetsAsync(JavaPackageDir, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Maven is not installed or not available in PATH"));
            });
        }

        [Test]
        public async Task TestUpdateSnippetsAsync_NoPomXml_ReturnsError()
        {
            // Arrange
            var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(emptyDir);
            
            SetupSuccessfulMavenVersionCheck();

            // Act
            var result = await LangService.UpdateSnippetsAsync(emptyDir, CancellationToken.None);
            
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
        public async Task TestUpdateSnippetsAsync_Success()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulSnippetUpdate();

            // Act
            var result = await LangService.UpdateSnippetsAsync(JavaPackageDir, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Is.EqualTo("Code snippets updated successfully"));
            });
        }

        [Test]
        public async Task TestUpdateSnippetsAsync_Failure()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupFailedSnippetUpdate();

            // Act
            var result = await LangService.UpdateSnippetsAsync(JavaPackageDir, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Code snippet update failed"));
                Assert.That(result.NextSteps, Is.Not.Null);
                Assert.That(result.NextSteps, Has.Count.EqualTo(1));
                Assert.That(result.NextSteps![0], Does.Contain("codesnippet-maven-plugin"));
            });
        }

        [Test]
        public async Task TestUpdateSnippetsAsync_ExceptionHandling()
        {
            // Arrange
            MockProcessHelper.Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Process execution failed"));

            // Act
            var result = await LangService.UpdateSnippetsAsync(JavaPackageDir, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Error during code snippet update: Process execution failed"));
            });
        }

        [Test]
        public async Task TestUpdateSnippetsAsync_VerifyCorrectMavenCommand()
        {
            // Arrange
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulSnippetUpdate();

            // Act
            await LangService.UpdateSnippetsAsync(JavaPackageDir, CancellationToken.None);

            // Assert - verify the correct Maven command was called with -am flag
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") &&
                p.Args.Any(arg => arg.Contains("com.azure.tools:codesnippet-maven-plugin:update-codesnippet")) &&
                p.Args.Contains("-f") &&
                p.Args.Contains(pomPath) &&
                p.WorkingDirectory == JavaPackageDir &&
                p.Timeout == TimeSpan.FromMinutes(5)), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Helper Methods for Cross-Platform Command Validation

        /// <summary>
        /// Checks if the ProcessOptions represents a Maven version check command.
        /// Handles both Unix (mvn --version) and Windows (cmd.exe /C mvn --version) patterns.
        /// </summary>
        private static bool IsMavenVersionCheck(ProcessOptions options) =>
            (options.Command == "mvn" && options.Args.Contains("--version")) ||
            (options.Command == "cmd.exe" && options.Args.Contains("mvn") && options.Args.Contains("--version"));

        /// <summary>
        /// Checks if the ProcessOptions represents a Maven install command (Azure SDK approach).
        /// Handles both Unix (mvn) and Windows (cmd.exe /C mvn) patterns.
        /// </summary>
        private static bool IsMavenInstallCommand(ProcessOptions options)
        {
            return (options.Command == "mvn" && options.Args.Contains("install")) ||
                   (options.Command == "cmd.exe" && options.Args.Contains("mvn") && options.Args.Contains("install"));
        }

        /// <summary>
        /// Checks if the ProcessOptions represents a Maven javadoc command.
        /// Handles both Unix (mvn) and Windows (cmd.exe /C mvn) patterns.
        /// </summary>
        private static bool IsJavadocCommand(ProcessOptions options)
        {
            return (options.Command == "mvn" && options.Args.Contains("javadoc:jar")) ||
                   (options.Command == "cmd.exe" && options.Args.Contains("mvn") && options.Args.Contains("javadoc:jar"));
        }

        #endregion

        #endregion
    }
}
