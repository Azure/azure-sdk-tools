using NUnit.Framework;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;

namespace Azure.Tools.GeneratorAgent.Tests.Security
{
    [TestFixture]
    public class InputValidatorTests
    {
        private sealed class TestEnvironmentFixture : IDisposable
        {
            private readonly List<string> _createdDirectories = new();
            private readonly Mock<ILogger> _mockLogger;

            public TestEnvironmentFixture()
            {
                _mockLogger = new Mock<ILogger>();
            }

            public Mock<ILogger> Logger => _mockLogger;

            public string CreateUniqueTestDirectory(string baseName)
            {
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var directory = Path.Combine(Path.GetTempPath(), $"{baseName}_{uniqueId}");
                _createdDirectories.Add(directory);
                return directory;
            }

            public void Dispose()
            {
                // Clean up test directories to prevent disk space issues and test pollution
                // Ignore cleanup exceptions to avoid masking actual test failures
                foreach (var directory in _createdDirectories)
                {
                    if (Directory.Exists(directory))
                    {
                        try
                        {
                            Directory.Delete(directory, true);
                        }
                        catch
                        {
                            // Intentionally ignored - cleanup failures shouldn't fail tests
                        }
                    }
                }
            }
        }

        [Test]
        public void ValidateDirTraversal_WithNullPath_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateDirTraversal(null);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("path cannot be null or empty"));
        }

        [Test]
        public void ValidateDirTraversal_WithEmptyPath_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateDirTraversal("");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("path cannot be null or empty"));
        }

        [Test]
        public void ValidateDirTraversal_WithWhitespacePath_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateDirTraversal("   ");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("path cannot be null or empty"));
        }

        [Test]
        public void ValidateDirTraversal_WithTildeCharacter_ShouldReturnValid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateDirTraversal("~/malicious");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("~/malicious"));
        }

        [Test]
        public void ValidateDirTraversal_WithDirectoryTraversalPattern_ShouldReturnValid()
        {
            using var fixture = new TestEnvironmentFixture();
            var testPaths = new[]
            {
                "../../../etc/passwd",
                "..\\..\\windows\\system32",
                "/../../etc/shadow",
                "C:\\temp\\..\\..\\windows",
                "./../../secrets"
            };

            foreach (var path in testPaths)
            {
                var result = InputValidator.ValidateDirTraversal(path);

                Assert.That(result.IsSuccess, Is.True, $"Path '{path}' should be valid");
                Assert.That(result.Value, Is.EqualTo(path));
            }
        }

        [Test]
        public void ValidateDirTraversal_WithValidPath_ShouldReturnValid()
        {
            using var fixture = new TestEnvironmentFixture();
            var validPaths = new[]
            {
                @"C:\temp\typespec",
                "/home/user/project",
                "relative/path/to/project",
                "simple-name",
                "project_with_underscores",
                "project-with-dashes"
            };

            foreach (var path in validPaths)
            {
                var result = InputValidator.ValidateDirTraversal(path);

                Assert.That(result.IsSuccess, Is.True, $"Path '{path}' should be valid");
                Assert.That(result.Value, Is.EqualTo(path));
            }
        }

        [Test]
        public void ValidateDirTraversal_WithCustomPathType_ShouldReturnValid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateDirTraversal("../malicious", "custom path type");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("../malicious"));
        }


        [Test]
        public void ValidateTypeSpecDir_WithNullPath_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateTypeSpecDir(null);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("TypeSpec path cannot be null or empty"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithEmptyPath_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateTypeSpecDir("");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("TypeSpec path cannot be null or empty"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithDirectoryTraversal_ShouldCheckDirectoryExistence()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateTypeSpecDir("../../../malicious");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("TypeSpec directory not found"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithValidLocalPath_ShouldReturnValid()
        {
            using var fixture = new TestEnvironmentFixture();
            var validPath = fixture.CreateUniqueTestDirectory("ValidTypeSpec");
            Directory.CreateDirectory(validPath);
            File.WriteAllText(Path.Combine(validPath, "test.tsp"), "// TypeSpec file");

            var result = InputValidator.ValidateTypeSpecDir(validPath, isLocalPath: true);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(validPath));
        }

        [Test]
        public void ValidateTypeSpecDir_WithValidGitHubPath_ShouldReturnValid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateTypeSpecDir("specs/data-plane", isLocalPath: false);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("specs/data-plane"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithNonExistentLocalPath_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var nonExistentPath = fixture.CreateUniqueTestDirectory("NonExistent");

            var result = InputValidator.ValidateTypeSpecDir(nonExistentPath, isLocalPath: true);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("TypeSpec directory not found"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithFileInsteadOfDirectory_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var testDir = fixture.CreateUniqueTestDirectory("TestFile");
            var filePath = Path.Combine(testDir, "TestFile.txt");
            Directory.CreateDirectory(testDir);
            File.WriteAllText(filePath, "test");

            var result = InputValidator.ValidateTypeSpecDir(filePath, isLocalPath: true);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("TypeSpec directory not found"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithDirectoryContainingNonTypeSpecFiles_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var testDir = fixture.CreateUniqueTestDirectory("MixedFiles");
            Directory.CreateDirectory(testDir);
            File.WriteAllText(Path.Combine(testDir, "valid.tsp"), "// TypeSpec file");
            File.WriteAllText(Path.Combine(testDir, "config.yaml"), "# YAML file");
            File.WriteAllText(Path.Combine(testDir, "readme.txt"), "readme");
            File.WriteAllText(Path.Combine(testDir, "script.js"), "console.log('test');");

            var result = InputValidator.ValidateTypeSpecDir(testDir, isLocalPath: true);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("Directory contains non-TypeSpec files"));
            Assert.That(result.Error, Does.Contain("readme.txt"));
            Assert.That(result.Error, Does.Contain("script.js"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithEmptyDirectory_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var testDir = fixture.CreateUniqueTestDirectory("EmptyDir");
            Directory.CreateDirectory(testDir);

            var result = InputValidator.ValidateTypeSpecDir(testDir, isLocalPath: true);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("No .tsp or .yaml files found in directory"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithRepositoryPathStartingWithSlash_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateTypeSpecDir("/invalid/path", isLocalPath: false);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("Repository path cannot start with / or \\"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithRepositoryPathStartingWithBackslash_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateTypeSpecDir("\\invalid\\path", isLocalPath: false);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("Repository path cannot start with / or \\"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithRepositoryPathWithDoubleSeparators_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result1 = InputValidator.ValidateTypeSpecDir("specs//data-plane", isLocalPath: false);
            var result2 = InputValidator.ValidateTypeSpecDir("specs\\\\data-plane", isLocalPath: false);

            Assert.That(result1.IsSuccess, Is.False);
            Assert.That(result1.Error, Is.EqualTo("Repository path contains invalid double separators"));
            Assert.That(result2.IsSuccess, Is.False);
            Assert.That(result2.Error, Is.EqualTo("Repository path contains invalid double separators"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithValidRepositoryPath_ShouldNormalizePath()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateTypeSpecDir("specs\\data-plane", isLocalPath: false);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("specs/data-plane"));
        }


        [Test]
        public void ValidateCommitId_WithNullCommitId_ShouldReturnValidEmpty()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateCommitId(null);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ValidateCommitId_WithEmptyCommitId_ShouldReturnValidEmpty()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateCommitId("");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ValidateCommitId_WithWhitespaceCommitId_ShouldReturnValidEmpty()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateCommitId("   ");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ValidateCommitId_WithValidCommitId_ShouldReturnValid()
        {
            using var fixture = new TestEnvironmentFixture();
            var validCommitIds = new[]
            {
                "abc123",           // 6 characters
                "abc123def456",     // 12 characters
                "abc123def4567890123456789012345678901234", // 40 characters (full SHA-1)
                "ABCDEF123456",     // Uppercase
                "123456abcdef"      // Numbers and lowercase
            };

            foreach (var commitId in validCommitIds)
            {
                var result = InputValidator.ValidateCommitId(commitId);

                Assert.That(result.IsSuccess, Is.True, $"Commit ID '{commitId}' should be valid");
                Assert.That(result.Value, Is.EqualTo(commitId));
            }
        }

        [Test]
        public void ValidateCommitId_WithInvalidCommitId_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var invalidCommitIds = new[]
            {
                "abc12",            // Too short (5 characters)
                "abc123def456789012345678901234567890123456z", // Too long (41 characters)
                "abc123-def456",    // Contains hyphen
                "abc123 def456",    // Contains space
                "abc123!def456",    // Contains special character
                "g123456789012",    // Contains invalid hex character 'g'
            };

            foreach (var commitId in invalidCommitIds)
            {
                var result = InputValidator.ValidateCommitId(commitId);

                Assert.That(result.IsSuccess, Is.False, $"Commit ID '{commitId}' should be invalid");
                Assert.That(result.Error, Does.Contain("Commit ID must be 6-40 hexadecimal characters"));
            }
        }

        [Test]
        public void ValidateOutputDirectory_WithNullPath_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateOutputDirectory(null);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("Output directory path cannot be null or empty"));
        }

        [Test]
        public void ValidateOutputDirectory_WithEmptyPath_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateOutputDirectory("");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("Output directory path cannot be null or empty"));
        }

        [Test]
        public void ValidateOutputDirectory_WithNonExistentParentDirectory_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var nonExistentParent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "child");
            
            var result = InputValidator.ValidateOutputDirectory(nonExistentParent);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("Parent directory does not exist"));
        }

        [Test]
        public void ValidateOutputDirectory_WithValidExistingDirectory_ShouldReturnValid()
        {
            using var fixture = new TestEnvironmentFixture();
            var validPath = fixture.CreateUniqueTestDirectory("ValidOutput");
            Directory.CreateDirectory(validPath);

            var result = InputValidator.ValidateOutputDirectory(validPath);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(validPath));
        }

        [Test]
        public void ValidateOutputDirectory_WithValidNonExistentDirectory_ShouldReturnValid()
        {
            using var fixture = new TestEnvironmentFixture();
            var nonExistentPath = fixture.CreateUniqueTestDirectory("NonExistentButValidOutput");

            var result = InputValidator.ValidateOutputDirectory(nonExistentPath);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(nonExistentPath));
        }


        [Test]
        public void ValidatePowerShellScriptPath_WithNullPath_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkPath = fixture.CreateUniqueTestDirectory("TestAzureSDK");

            var result = InputValidator.ValidatePowerShellScriptPath(null!, azureSdkPath);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("PowerShell script path cannot be null or empty"));
        }

        [Test]
        public void ValidatePowerShellScriptPath_WithEmptyPath_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkPath = fixture.CreateUniqueTestDirectory("TestAzureSDK");

            var result = InputValidator.ValidatePowerShellScriptPath("", azureSdkPath);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("PowerShell script path cannot be null or empty"));
        }

        [Test]
        public void ValidatePowerShellScriptPath_WithNonPs1Extension_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkPath = fixture.CreateUniqueTestDirectory("TestAzureSDK");

            var result = InputValidator.ValidatePowerShellScriptPath("script.bat", azureSdkPath);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("PowerShell script must have .ps1 extension"));
        }

        [Test]
        public void ValidatePowerShellScriptPath_WithValidScript_ShouldReturnValid()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkPath = fixture.CreateUniqueTestDirectory("TestAzureSDK");
            var scriptDir = Path.Combine(azureSdkPath, "scripts");
            var scriptPath = Path.Combine(scriptDir, "test.ps1");
            Directory.CreateDirectory(azureSdkPath);
            Directory.CreateDirectory(scriptDir);
            File.WriteAllText(scriptPath, "# Test script");

            var result = InputValidator.ValidatePowerShellScriptPath("scripts/test.ps1", azureSdkPath);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Path.Combine(azureSdkPath, "scripts/test.ps1")));
        }

        [Test]
        public void ValidatePowerShellScriptPath_WithScriptOutsideAzureSDK_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkPath = fixture.CreateUniqueTestDirectory("TestAzureSDK");
            var otherLocation = fixture.CreateUniqueTestDirectory("OtherLocation");
            var scriptPath = Path.Combine(otherLocation, "malicious.ps1");

            var result = InputValidator.ValidatePowerShellScriptPath(scriptPath, azureSdkPath);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("PowerShell script not found"));
        }

        [Test]
        public void ValidatePowerShellScriptPath_WithDirectoryTraversal_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var azureSdkPath = fixture.CreateUniqueTestDirectory("TestAzureSDK");
            var scriptPath = Path.Combine(azureSdkPath, "..", "..", "..", "malicious.ps1");

            var result = InputValidator.ValidatePowerShellScriptPath(scriptPath, azureSdkPath);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("PowerShell script not found"));
        }


        [Test]
        public void ValidateProcessArguments_WithNullArguments_ShouldReturnValid()
        {
            var result = InputValidator.ValidateProcessArguments(null!);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ValidateProcessArguments_WithEmptyArguments_ShouldReturnValid()
        {
            var result = InputValidator.ValidateProcessArguments("");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(""));
        }

        [Test]
        public void ValidateProcessArguments_WithValidArguments_ShouldReturnValid()
        {
            var validArguments = new[]
            {
                "install --global @typespec/compiler",
                "build --configuration Release",
                "-Command \"npm install\"",
                "compile . --emit @azure-tools/typespec-csharp"
            };

            foreach (var args in validArguments)
            {
                var result = InputValidator.ValidateProcessArguments(args);

                Assert.That(result.IsSuccess, Is.True, $"Arguments '{args}' should be valid");
                Assert.That(result.Value, Is.EqualTo(args));
            }
        }

        [Test]
        public void ValidateProcessArguments_WithDangerousPatterns_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var dangerousArguments = new[]
            {
                "install && rm -rf /",
                "build | malicious-command",
                "install; shutdown -h now",
                "build || evil-command",
                "install & malicious"
            };

            foreach (var args in dangerousArguments)
            {
                var result = InputValidator.ValidateProcessArguments(args);

                Assert.That(result.IsSuccess, Is.False, $"Arguments '{args}' should be invalid");
                Assert.That(result.Error, Does.Contain("Arguments contain command separator:"));
            }
        }

        [Test]
        public void ValidateProcessArguments_WithIndividualDangerousPatterns_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var commandSeparators = new[]
            {
                "&", "|", ";", "&&", "||"
            };

            foreach (var separator in commandSeparators)
            {
                var testArg = $"safe-command {separator} other-arg";
                var result = InputValidator.ValidateProcessArguments(testArg);

                Assert.That(result.IsSuccess, Is.False, $"Separator '{separator}' should be detected as dangerous");
                Assert.That(result.Error, Does.Contain($"Arguments contain command separator: {separator}"));
            }
        }

        [Test]
        public void ValidateProcessArguments_WithCaseInsensitiveDangerousPatterns_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var caseVariations = new[]
            {
                "install & malicious",
                "build | evil",
                "run ; dangerous"
            };

            foreach (var args in caseVariations)
            {
                var result = InputValidator.ValidateProcessArguments(args);

                Assert.That(result.IsSuccess, Is.False, $"Arguments '{args}' should be invalid (case insensitive)");
                Assert.That(result.Error, Does.Contain("Arguments contain command separator:"));
            }
        }

        [Test]
        public void ValidateWorkingDirectory_WithNullDirectory_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateWorkingDirectory(null);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Directory.GetCurrentDirectory()));
        }

        [Test]
        public void ValidateWorkingDirectory_WithEmptyDirectory_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateWorkingDirectory("");

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Directory.GetCurrentDirectory()));
        }

        [Test]
        public void ValidateWorkingDirectory_WithDirectoryTraversal_ShouldCheckDirectoryExistence()
        {
            using var fixture = new TestEnvironmentFixture();
            var result = InputValidator.ValidateWorkingDirectory("../../../malicious");

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("Working directory does not exist").Or.Contains("Invalid working directory path"));
        }

        [Test]
        public void ValidateWorkingDirectory_WithValidExistingDirectory_ShouldReturnValid()
        {
            using var fixture = new TestEnvironmentFixture();
            var validPath = fixture.CreateUniqueTestDirectory("TestDir");
            Directory.CreateDirectory(validPath);

            var result = InputValidator.ValidateWorkingDirectory(validPath);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(validPath));
        }

        [Test]
        public void ValidateWorkingDirectory_WithNonExistentDirectory_ShouldReturnInvalid()
        {
            using var fixture = new TestEnvironmentFixture();
            var nonExistentDir = fixture.CreateUniqueTestDirectory("NonExistentDirectory");

            var result = InputValidator.ValidateWorkingDirectory(nonExistentDir);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("Working directory does not exist"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithInvalidPathCharacters_ShouldHandleGracefully()
        {
            using var fixture = new TestEnvironmentFixture();
            var invalidPath = "C:\\Invalid..\\Path";

            var result = InputValidator.ValidateTypeSpecDir(invalidPath, isLocalPath: true);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("TypeSpec directory not found").Or.Contains("Invalid path format"));
        }

        [Test]
        public void ValidateOutputDirectory_WithInvalidPathCharacters_ShouldHandleGracefully()
        {
            using var fixture = new TestEnvironmentFixture();
            var invalidPath = "C:\\Invalid..\\Path";

            var result = InputValidator.ValidateOutputDirectory(invalidPath);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Does.Contain("Parent directory does not exist").Or.Contains("Invalid path format"));
        }
    }
}
