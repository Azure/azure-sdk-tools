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

        #region LintCodeAsync Tests

        [Test]
        public async Task TestLintCodeAsync_MavenNotInstalled_ReturnsError()
        {
            // Arrange - Reset mock to avoid interference from other test setups
            MockProcessHelper.Reset();
            
            // Mock Maven version check - match both Unix and Windows patterns
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" && p.Args.Contains("--version")) || 
                (p.Command == "cmd.exe" && p.Args.Contains("mvn") && p.Args.Contains("--version"))), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "Maven not found")] });

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
            // Arrange - Reset mock to avoid interference from other test setups
            MockProcessHelper.Reset();
            
            // Mock Maven version check - match both Unix and Windows patterns
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" && p.Args.Contains("--version")) || 
                (p.Command == "cmd.exe" && p.Args.Contains("mvn") && p.Args.Contains("--version"))), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Apache Maven 3.9.9")] });

            var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(emptyDir);
            try { Directory.Delete(emptyDir, true); } catch { }

            // Act
            var result = await LangService.LintCodeAsync(emptyDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("No pom.xml found"));
            });
        }

        [Test]
        public async Task TestLintCodeAsync_AllToolsPass_ReturnsSuccess()
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
                        // Simulate successful Azure SDK-style Maven install with clean linting output
                        return new ProcessResult { 
                            ExitCode = 0, 
                            OutputDetails = [(StdioLevel.StandardOutput, 
                                "[INFO] BUILD SUCCESS\n" +
                                "[INFO] Total time: 30.123 s\n" +
                                "[INFO] Finished at: 2025-01-09T10:30:00-08:00"
                            )] 
                        };
                    }
                    
                    return new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "Unknown command")] };
                });

            // Act
            var result = await LangService.LintCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.CheckStatusDetails, Does.Contain("Code linting passed"));
                Assert.That(result.CheckStatusDetails, Does.Contain("All tools successful"));
                Assert.That(result.CheckStatusDetails, Does.Contain("Checkstyle, SpotBugs, RevAPI"));
            });

            // Verify captured commands - Azure SDK approach uses install, not individual plugin calls
            Assert.That(capturedOptions, Has.Count.EqualTo(2)); // Maven version + install command  
            Assert.That(capturedOptions.Any(IsMavenVersionCheck), Is.True, "Maven version check should be called");
            Assert.That(capturedOptions.Any(IsMavenInstallCommand), Is.True, "Maven install should be called");
        }

        [Test]
        public async Task TestLintCodeAsync_CheckstyleFails_ReturnsError()
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
                        var errorOutput = "[ERROR] Failed to execute goal com.puppycrawl.tools:checkstyle:9.3:check (default) on project test: You have 5 Checkstyle violations.";
                        return new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, errorOutput)] };
                    }
                    
                    return new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "Unknown command")] };
                });

            // Act
            var result = await LangService.LintCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert - Azure SDK approach: Maven install failed with Checkstyle errors
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Code linting found issues"));
                Assert.That(result.ResponseError, Does.Contain("Checkstyle"));
                Assert.That(result.CheckStatusDetails, Does.Contain("checkstyle"));
            });

            // Verify captured commands - Azure SDK approach uses install
            Assert.That(capturedOptions, Has.Count.EqualTo(2)); // Maven version + install command
            Assert.That(capturedOptions.Any(IsMavenVersionCheck), Is.True, "Maven version check should be called");
            Assert.That(capturedOptions.Any(IsMavenInstallCommand), Is.True, "Maven install should be called");
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
                    
                    return new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "Unknown command")] };
                });

            // Act
            var result = await LangService.LintCodeAsync(JavaPackageDir, false, CancellationToken.None);

            // Assert - Azure SDK approach: All tools failed in single Maven install command  
            Assert.Multiple(() =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.ResponseError, Does.Contain("Code linting found issues"));
                Assert.That(result.ResponseError, Does.Contain("Checkstyle, SpotBugs, RevAPI"));
                Assert.That(result.CheckStatusDetails, Does.Contain("checkstyle"));
                Assert.That(result.CheckStatusDetails, Does.Contain("spotbugs-maven-plugin"));
                Assert.That(result.CheckStatusDetails, Does.Contain("revapi-maven-plugin"));
            });

            // Verify captured commands - Azure SDK approach uses single install
            Assert.That(capturedOptions, Has.Count.EqualTo(2)); // Maven version + install command
            Assert.That(capturedOptions.Any(IsMavenVersionCheck), Is.True, "Maven version check should be called");
            Assert.That(capturedOptions.Any(IsMavenInstallCommand), Is.True, "Maven install should be called");
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
                    
                    return new ProcessResult { ExitCode = 1, OutputDetails = [(StdioLevel.StandardError, "Unknown command")] };
                });

            // Act
            await LangService.LintCodeAsync(JavaPackageDir, true, CancellationToken.None);

            // Assert - verify the Maven install command was called with fix parameters
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
        public async Task TestLintCodeAsync_Exception_ReturnsError()
        {
            // Arrange - Reset mock to avoid interference from other test setups
            MockProcessHelper.Reset();
            
            // Mock Maven version check - match both Unix and Windows patterns
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" && p.Args.Contains("--version")) || 
                (p.Command == "cmd.exe" && p.Args.Contains("mvn") && p.Args.Contains("--version"))), 
                It.IsAny<CancellationToken>()))
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
            // Arrange - Reset mock to avoid interference from other test setups
            MockProcessHelper.Reset();
            
            var pomPath = Path.Combine(JavaPackageDir, "pom.xml");
            // Mock Maven version check - match both Unix and Windows patterns
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" && p.Args.Contains("--version")) || 
                (p.Command == "cmd.exe" && p.Args.Contains("mvn") && p.Args.Contains("--version"))), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "Apache Maven 3.9.9")] });

            // Setup Azure SDK style Maven install to pass - match both Unix and Windows patterns
            MockProcessHelper.Setup(x => x.Run(It.Is<ProcessOptions>(p => 
                (p.Command == "mvn" && p.Args.Contains("install")) || 
                (p.Command == "cmd.exe" && p.Args.Contains("mvn") && p.Args.Contains("install"))), 
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult { ExitCode = 0, OutputDetails = [(StdioLevel.StandardOutput, "BUILD SUCCESS")] });

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

        #region Helper Methods for Cross-Platform Command Validation

        /// <summary>
        /// Checks if the ProcessOptions represents a Maven version check command.
        /// Handles both Unix (mvn --version) and Windows (cmd.exe /C mvn --version) patterns.
        /// </summary>
        private static bool IsMavenVersionCheck(ProcessOptions options) =>
            (options.Command == "mvn" && options.Args.Contains("--version")) ||
            (options.Command == "cmd.exe" && options.Args.Contains("mvn") && options.Args.Contains("--version"));

        /// <summary>
        /// Checks if the ProcessOptions represents a Checkstyle command.
        /// Handles both Unix and Windows patterns.
        /// </summary>
        private static bool IsCheckstyleCommand(ProcessOptions options) =>
            (options.Command == "mvn" && options.Args.Contains("checkstyle:check")) ||
            (options.Command == "cmd.exe" && options.Args.Contains("mvn") && options.Args.Contains("checkstyle:check"));

        /// <summary>
        /// Checks if the ProcessOptions represents a SpotBugs command.
        /// Handles both Unix and Windows patterns.
        /// </summary>
        private static bool IsSpotBugsCommand(ProcessOptions options) =>
            (options.Command == "mvn" && options.Args.Contains("spotbugs:check")) ||
            (options.Command == "cmd.exe" && options.Args.Contains("mvn") && options.Args.Contains("spotbugs:check"));

        /// <summary>
        /// Checks if the ProcessOptions represents a RevAPI command.
        /// Handles both Unix and Windows patterns.
        /// </summary>
        private static bool IsRevAPICommand(ProcessOptions options) =>
            (options.Command == "mvn" && options.Args.Contains("revapi:check")) ||
            (options.Command == "cmd.exe" && options.Args.Contains("mvn") && options.Args.Contains("revapi:check"));

        /// <summary>
        /// Checks if the ProcessOptions represents a Checkstyle command with fix parameters.
        /// Handles both Unix (mvn) and Windows (cmd.exe /C mvn) patterns.
        /// </summary>
        private static bool IsCheckstyleFixCommand(ProcessOptions options)
        {
            if (IsCheckstyleCommand(options))
            {
                return options.Args.Any(arg => arg.Contains("-Dcheckstyle.failOnViolation=false")) &&
                       options.Args.Any(arg => arg.Contains("-Dcheckstyle.failsOnError=false"));
            }
            return false;
        }

        /// <summary>
        /// Checks if the ProcessOptions represents a SpotBugs command with fix parameters.
        /// Handles both Unix (mvn) and Windows (cmd.exe /C mvn) patterns.
        /// </summary>
        private static bool IsSpotBugsFixCommand(ProcessOptions options)
        {
            if (IsSpotBugsCommand(options))
            {
                return options.Args.Any(arg => arg.Contains("-Dspotbugs.failOnViolation=false")) &&
                       options.Args.Any(arg => arg.Contains("-Dspotbugs.failsOnError=false"));
            }
            return false;
        }

        /// <summary>
        /// Checks if the ProcessOptions represents a RevAPI command with fix parameters.
        /// Handles both Unix (mvn) and Windows (cmd.exe /C mvn) patterns.
        /// </summary>
        private static bool IsRevAPIFixCommand(ProcessOptions options)
        {
            if (IsRevAPICommand(options))
            {
                return options.Args.Any(arg => arg.Contains("-Drevapi.failOnViolation=false")) &&
                       options.Args.Any(arg => arg.Contains("-Drevapi.failsOnError=false"));
            }
            return false;
        }

        /// <summary>
        /// Checks if the ProcessOptions represents a Maven install command (Azure SDK approach).
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
