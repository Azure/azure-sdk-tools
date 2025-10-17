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
                new Mock<IGitHelper>().Object,
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
        /// Sets up successful Maven install command.
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
        /// Sets up successful Maven linting command with all tools including javadoc.
        /// This matches the fail-safe accumulation approach.
        /// </summary>
        private void SetupSuccessfulMavenLinting()
        {
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") && 
                p.Args.Contains("install") &&
                p.Args.Contains("--no-transfer-progress") &&
                p.Args.Contains("-DskipTests") &&
                p.Args.Contains("-Dgpg.skip") &&
                p.Args.Contains("-DtrimStackTrace=false") &&
                p.Args.Contains("-Dmaven.javadoc.skip=false") &&
                p.Args.Contains("-Dcodesnippet.skip=true") &&
                p.Args.Contains("-Dspotless.skip=false") &&
                p.Args.Contains("-Djacoco.skip=true") &&
                p.Args.Contains("-Dshade.skip=true") &&
                p.Args.Contains("-Dmaven.antrun.skip=true") &&
                p.Args.Contains("-Dcheckstyle.failOnViolation=false") &&
                p.Args.Contains("-Dcheckstyle.failsOnError=false") &&
                p.Args.Contains("-Dspotbugs.failOnError=false") &&
                p.Args.Contains("-Drevapi.failBuildOnProblemsFound=false") &&
                p.Args.Contains("-am") &&
                p.Args.Contains("-f") &&
                p.Args.Contains(pomPath) &&
                p.WorkingDirectory == JavaPackageDir &&
                p.Timeout == TimeSpan.FromMinutes(15)), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { 
                    ExitCode = 0, 
                    OutputDetails = [(StdioLevel.StandardOutput, 
                        "[INFO] Building jar: /path/to/target/test-1.0-javadoc.jar\n" +
                        "[INFO] BUILD SUCCESS"
                    )] 
                });
        }

        #endregion
        
        [Test]
        public async Task TestFormatCode_MavenNotAvailable_ReturnsError()
        {
            // Arrange
            SetupFailedMavenVersionCheck();

            // Act
            var result = await LangService.FormatCode(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Maven is not installed or not available in PATH"));
            });
        }

        [Test]
        public async Task TestFormatCode_NoPomXml_ReturnsError()
        {
            // Arrange - Use a temp directory without pom.xml for this test
            var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(emptyDir);
            
            SetupSuccessfulMavenVersionCheck();

            // Act
            var result = await LangService.FormatCode(emptyDir, false, CancellationToken.None);
            
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
        public async Task TestFormatCode_CheckMode_Success()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulSpotlessCheck();

            // Act
            var result = await LangService.FormatCode(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Is.EqualTo("Code formatting check passed - all files are properly formatted"));
            });
        }

        [Test]
        public async Task TestFormatCode_ApplyMode_Success()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulSpotlessApply();

            // Act
            var result = await LangService.FormatCode(JavaPackageDir, true, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Is.EqualTo("Code formatting applied successfully"));
            });
        }

        [Test]
        public async Task TestFormatCode_CheckMode_FormattingNeeded()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupFailedSpotlessCheck();

            // Act
            var result = await LangService.FormatCode(JavaPackageDir, false, CancellationToken.None);

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
        public async Task TestFormatCode_ApplyMode_Failure()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupFailedSpotlessApply();

            // Act
            var result = await LangService.FormatCode(JavaPackageDir, true, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.CheckStatusDetails, Does.Contain("spotless failed with errors"));
                Assert.That(result.ResponseError, Does.Contain("Code formatting failed to apply"));
            });
        }

        [Test]
        public async Task TestFormatCode_NoPomInPackageDirectory_ReturnsError()
        {
            // Arrange
            var subDir = Path.Combine(JavaPackageDir, "src", "main", "java");
            Directory.CreateDirectory(subDir);
            
            // Create pom.xml in parent directory but not in the package directory we're testing
            var parentPomPath = Path.Combine(JavaPackageDir, "pom.xml");
            File.WriteAllText(parentPomPath, "<project></project>");

            SetupSuccessfulMavenVersionCheck();

            // Act - run from subdirectory where there's no pom.xml
            var result = await LangService.FormatCode(subDir, false, CancellationToken.None);

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
        public async Task TestFormatCode_ExceptionHandling()
        {
            // Arrange
            MockProcessHelper.Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("Process execution failed"));

            // Act
            var result = await LangService.FormatCode(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Error during code formatting: Process execution failed"));
            });
        }

        [Test]
        public async Task TestFormatCode_VerifyCorrectMavenCommand_CheckMode()
        {
            // Arrange
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulSpotlessCheck();

            // Act
            await LangService.FormatCode(JavaPackageDir, false, CancellationToken.None);

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
        public async Task TestFormatCode_VerifyCorrectMavenCommand_ApplyMode()
        {
            // Arrange
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulSpotlessApply();

            // Act
            await LangService.FormatCode(JavaPackageDir, true, CancellationToken.None);

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

        #region LintCode Tests

        [Test]
        public async Task TestLintCode_MavenNotInstalled_ReturnsError()
        {
            // Arrange
            SetupFailedMavenVersionCheck();

            // Act
            var result = await LangService.LintCode(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Maven is not installed or not available in PATH"));
            });
        }

        [Test]
        public async Task TestLintCode_NoPomXml_ReturnsError()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            
            var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(emptyDir);

            try
            {
                // Act
                var result = await LangService.LintCode(emptyDir, false, CancellationToken.None);

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
        public async Task TestLintCode_AllToolsPass_ReturnsSuccess()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulMavenLinting();

            // Act
            var result = await LangService.LintCode(JavaPackageDir, false, CancellationToken.None);

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
        }

        [Test]
        public async Task TestLintCode_CheckstyleFails_ReturnsError()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            var checkstyleErrorOutput = "[ERROR] Failed to execute goal com.puppycrawl.tools:checkstyle:9.3:check (default) on project test: You have 5 Checkstyle violations.";
            SetupMavenInstallWithToolErrors(checkstyleErrorOutput);

            // Act
            var result = await LangService.LintCode(JavaPackageDir, false, CancellationToken.None);

            // Assert - Maven install failed with Checkstyle errors
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

            // Verify Maven commands were called correctly (no separate javadoc command)
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenVersionCheck(p)), It.IsAny<CancellationToken>()), Times.Once);
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenInstallCommand(p)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task TestLintCode_AllToolsFail_ReturnsError()
        {
            // Arrange
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
                                        "[ERROR] Failed to execute goal org.revapi:revapi-maven-plugin:0.15.1:check (default) on project test: API problems detected\n" +
                                        "[ERROR] Failed to execute goal org.apache.maven.plugins:maven-javadoc-plugin:3.4.1:jar (default) on project test: MavenReportException: Error while generating Javadoc";
                        return new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, errorOutput)] };
                    }
                    
                    return new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "Unknown command")] };
                });

            // Act
            var result = await LangService.LintCode(JavaPackageDir, false, CancellationToken.None);

            // Assert - All tools failed
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Code linting found issues"));
                Assert.That(result.ResponseError, Does.Contain("Checkstyle, SpotBugs, RevAPI, Javadoc"));
                Assert.That(result.CheckStatusDetails, Does.Contain("checkstyle"));
                Assert.That(result.CheckStatusDetails, Does.Contain("spotbugs-maven-plugin"));
                Assert.That(result.CheckStatusDetails, Does.Contain("revapi-maven-plugin"));
                Assert.That(result.CheckStatusDetails, Does.Contain("maven-javadoc-plugin"));
                Assert.That(result.NextSteps, Is.Not.Null);
                Assert.That(result.NextSteps, Has.Count.GreaterThan(0));
                Assert.That(result.NextSteps!.Any(step => step.Contains("Checkstyle")), Is.True);
                Assert.That(result.NextSteps!.Any(step => step.Contains("SpotBugs")), Is.True);
                Assert.That(result.NextSteps!.Any(step => step.Contains("RevAPI")), Is.True);
                Assert.That(result.NextSteps!.Any(step => step.Contains("Javadoc")), Is.True);
            });

            // Verify captured commands - Maven commands with javadoc included
            Assert.That(capturedOptions, Has.Count.EqualTo(2)); // Maven version + install command
            Assert.That(capturedOptions.Any(IsMavenVersionCheck), Is.True, "Maven version check should be called");
            Assert.That(capturedOptions.Any(IsMavenInstallCommand), Is.True, "Maven install should be called");
        }

        [Test]
        public async Task TestLintCode_WithFix_UsesFailOnViolationFalse()
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
                    
                    return new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "Unknown command")] };
                });

            // Act
            await LangService.LintCode(JavaPackageDir, true, CancellationToken.None);

            // Assert - verify the Maven command was called with fix parameters
            Assert.That(capturedOptions, Has.Count.EqualTo(2)); // Maven version + install command
            Assert.That(capturedOptions.Any(IsMavenVersionCheck), Is.True, "Maven version check should be called");
            Assert.That(capturedOptions.Any(IsMavenInstallCommand), Is.True, "Maven install should be called");
            
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
        public async Task TestLintCode_Exception_ReturnsError()
        {
            // Arrange
            MockProcessHelper.Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Process execution failed"));

            // Act
            var result = await LangService.LintCode(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Error during code linting: Process execution failed"));
            });
        }

        [Test]
        public async Task TestLintCode_VerifyCorrectMavenCommands()
        {
            // Arrange
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulMavenLinting();

            // Act
            await LangService.LintCode(JavaPackageDir, false, CancellationToken.None);

            // Assert - verify Maven install command includes javadoc and fail-safe flags
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") &&
                p.Args.Contains("install") &&
                p.Args.Contains("--no-transfer-progress") &&
                p.Args.Contains("-DskipTests") &&
                p.Args.Contains("-Dgpg.skip") &&
                p.Args.Contains("-DtrimStackTrace=false") &&
                p.Args.Contains("-Dmaven.javadoc.skip=false") && // Javadoc included in install approach
                p.Args.Contains("-Dcodesnippet.skip=true") &&
                p.Args.Contains("-Dspotless.skip=false") &&
                p.Args.Contains("-Djacoco.skip=true") &&
                p.Args.Contains("-Dshade.skip=true") &&
                p.Args.Contains("-Dmaven.antrun.skip=true") &&
                p.Args.Contains("-Dcheckstyle.failOnViolation=false") && // Fail-safe flags
                p.Args.Contains("-Dcheckstyle.failsOnError=false") &&
                p.Args.Contains("-Dspotbugs.failOnError=false") &&
                p.Args.Contains("-Drevapi.failBuildOnProblemsFound=false") &&
                p.Args.Contains("-am") &&
                p.Args.Contains("-f") &&
                p.Args.Contains(pomPath) &&
                p.WorkingDirectory == JavaPackageDir &&
                p.Timeout == TimeSpan.FromMinutes(15)), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task TestLintCode_JavadocFails_ReturnsError()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            var javadocErrorOutput = "[ERROR] Failed to execute goal org.apache.maven.plugins:maven-javadoc-plugin:3.4.1:jar (default) on project test: MavenReportException: Error while generating Javadoc";
            SetupMavenInstallWithToolErrors(javadocErrorOutput); // Javadoc fails within install command

            // Act
            var result = await LangService.LintCode(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Code linting found issues"));
                Assert.That(result.ResponseError, Does.Contain("Javadoc"));
                Assert.That(result.CheckStatusDetails, Does.Contain("maven-javadoc-plugin")); // Javadoc errors within install output
                Assert.That(result.NextSteps, Is.Not.Null);
                Assert.That(result.NextSteps!.Any(step => step.Contains("Javadoc")), Is.True);
            });

            // Verify Maven commands were called correctly
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenVersionCheck(p)), It.IsAny<CancellationToken>()), Times.Once);
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenInstallCommand(p)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task TestLintCode_CheckstyleAndJavadocFail_ReturnsError()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            var combinedErrorOutput = "[ERROR] Failed to execute goal com.puppycrawl.tools:checkstyle:9.3:check (default) on project test: You have 5 Checkstyle violations.\n" +
                                    "[ERROR] Failed to execute goal org.apache.maven.plugins:maven-javadoc-plugin:3.4.1:jar (default) on project test: MavenReportException: Error while generating Javadoc";
            SetupMavenInstallWithToolErrors(combinedErrorOutput); // Both Checkstyle and Javadoc fail within install command

            // Act
            var result = await LangService.LintCode(JavaPackageDir, false, CancellationToken.None);

            // Assert - Both Checkstyle and Javadoc should report issues
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Code linting found issues"));
                Assert.That(result.ResponseError, Does.Contain("Checkstyle, Javadoc"));
                Assert.That(result.CheckStatusDetails, Does.Contain("checkstyle"));
                Assert.That(result.CheckStatusDetails, Does.Contain("maven-javadoc-plugin"));
                Assert.That(result.NextSteps, Is.Not.Null);
                Assert.That(result.NextSteps!.Any(step => step.Contains("Checkstyle")), Is.True);
                Assert.That(result.NextSteps!.Any(step => step.Contains("Javadoc")), Is.True);
                Assert.That(result.NextSteps!.Any(step => step.Contains("-Dmaven.javadoc.skip=true")), Is.True);
            });

            // Verify Maven commands were called correctly
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenVersionCheck(p)), It.IsAny<CancellationToken>()), Times.Once);
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => IsMavenInstallCommand(p)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task TestLintCodeAsync_CommandParameters_AreCorrect()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulMavenLinting();

            // Act
            await LangService.LintCode(JavaPackageDir, false, CancellationToken.None);

            // Assert - verify the Maven install command includes javadoc and all required parameters
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") &&
                p.Args.Contains("--no-transfer-progress") &&
                p.Args.Contains("install") &&
                p.Args.Contains("-Dmaven.javadoc.skip=false") && // Javadoc included in install approach
                p.Args.Contains("-DskipTests") &&
                p.Args.Contains("-Dgpg.skip") &&
                p.Args.Contains("-DtrimStackTrace=false") &&
                p.Args.Contains("-Dcodesnippet.skip=true") &&
                p.Args.Contains("-Dspotless.skip=false") &&
                p.Args.Contains("-Djacoco.skip=true") &&
                p.Args.Contains("-Dshade.skip=true") &&
                p.Args.Contains("-Dmaven.antrun.skip=true") &&
                p.Args.Contains("-Dcheckstyle.failOnViolation=false") && // Fail-safe flags
                p.Args.Contains("-Dcheckstyle.failsOnError=false") &&
                p.Args.Contains("-Dspotbugs.failOnError=false") &&
                p.Args.Contains("-Drevapi.failBuildOnProblemsFound=false") &&
                p.Args.Contains("-am") &&
                p.Args.Contains("-f") &&
                p.Args.Contains(pomPath) &&
                p.WorkingDirectory == JavaPackageDir &&
                p.Timeout == TimeSpan.FromMinutes(15)), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #region UpdateSnippets Tests

        [Test]
        public async Task TestUpdateSnippets_MavenNotAvailable_ReturnsError()
        {
            // Arrange
            SetupFailedMavenVersionCheck();

            // Act
            var result = await LangService.UpdateSnippets(JavaPackageDir, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Maven is not installed or not available in PATH"));
            });
        }

        [Test]
        public async Task TestUpdateSnippets_NoPomXml_ReturnsError()
        {
            // Arrange
            var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(emptyDir);
            
            SetupSuccessfulMavenVersionCheck();

            // Act
            var result = await LangService.UpdateSnippets(emptyDir, CancellationToken.None);
            
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
        public async Task TestUpdateSnippets_Success()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulSnippetUpdate();

            // Act
            var result = await LangService.UpdateSnippets(JavaPackageDir, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Is.EqualTo("Code snippets updated successfully"));
            });
        }

        [Test]
        public async Task TestUpdateSnippets_Failure()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupFailedSnippetUpdate();

            // Act
            var result = await LangService.UpdateSnippets(JavaPackageDir, CancellationToken.None);

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
        public async Task TestUpdateSnippets_ExceptionHandling()
        {
            // Arrange
            MockProcessHelper.Setup(x => x.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("Process execution failed"));

            // Act
            var result = await LangService.UpdateSnippets(JavaPackageDir, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Error during code snippet update: Process execution failed"));
            });
        }

        [Test]
        public async Task TestUpdateSnippets_VerifyCorrectMavenCommand()
        {
            // Arrange
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulSnippetUpdate();

            // Act
            await LangService.UpdateSnippets(JavaPackageDir, CancellationToken.None);

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

        #region AnalyzeDependenciesAsync Tests

        /// <summary>
        /// Sets up successful Maven dependency tree analysis.
        /// </summary>
        private void SetupSuccessfulDependencyAnalysis()
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") && 
                p.Args.Contains("dependency:tree") && 
                p.Args.Contains("-Dverbose")), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { 
                    ExitCode = 0, 
                    OutputDetails = [(StdioLevel.StandardOutput, "[INFO] BUILD SUCCESS\n[INFO] Total time: 5.123 s")] 
                });
        }

        /// <summary>
        /// Sets up failed Maven dependency tree analysis with conflicts.
        /// </summary>
        private void SetupFailedDependencyAnalysis()
        {
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") && 
                p.Args.Contains("dependency:tree") && 
                p.Args.Contains("-Dverbose")), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { 
                    ExitCode = 1, 
                    OutputDetails = [(StdioLevel.StandardError, "[ERROR] Failed to execute goal on project: Maven dependency analysis failed")] 
                });
        }

        [Test]
        public async Task TestAnalyzeDependenciesAsync_Success()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupSuccessfulDependencyAnalysis();

            // Act
            var result = await LangService.AnalyzeDependenciesAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Does.Contain("Dependency analysis completed - no conflicts detected"));
                Assert.That(result.NextSteps, Is.Null.Or.Empty);
            });

            // Verify correct Maven command was called
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") &&
                p.Args.Contains("dependency:tree") &&
                p.Args.Contains("-Dverbose") &&
                p.Args.Contains("-f") &&
                p.Args.Contains(pomPath) &&
                p.WorkingDirectory == JavaPackageDir &&
                p.Timeout == TimeSpan.FromMinutes(5)), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task TestAnalyzeDependenciesAsync_Failure()
        {
            // Arrange
            SetupSuccessfulMavenVersionCheck();
            SetupFailedDependencyAnalysis();

            // Act
            var result = await LangService.AnalyzeDependenciesAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Dependency analysis found issues"));
                Assert.That(result.NextSteps, Is.Not.Null);
                Assert.That(result.NextSteps, Has.Count.GreaterThan(0));
                Assert.That(result.NextSteps!.Any(step => step.Contains("Azure SDK BOM")), Is.True);
                Assert.That(result.NextSteps!.Any(step => step.Contains("github.com/Azure/azure-sdk-for-java")), Is.True);
            });

            // Verify correct Maven command was called
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            MockProcessHelper.Verify(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" || p.Command == "cmd.exe") &&
                p.Args.Contains("dependency:tree") &&
                p.Args.Contains("-Dverbose") &&
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
        /// Checks if the ProcessOptions represents a Maven install command.
        /// Handles both Unix (mvn) and Windows (cmd.exe /C mvn) patterns.
        /// </summary>
        private static bool IsMavenInstallCommand(ProcessOptions options)
        {
            return (options.Command == "mvn" && options.Args.Contains("install")) ||
                   (options.Command == "cmd.exe" && options.Args.Contains("mvn") && options.Args.Contains("install"));
        }

        #endregion

        #endregion
    }
}
