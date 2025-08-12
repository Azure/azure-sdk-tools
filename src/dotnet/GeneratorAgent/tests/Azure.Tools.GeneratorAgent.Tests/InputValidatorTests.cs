using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Azure.Tools.GeneratorAgent;
using Azure.Tools.GeneratorAgent.Security;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests.Security
{
    [TestFixture]
    public class InputValidatorTests
    {
        #region Test Infrastructure

        private sealed class TestEnvironmentFixture : IDisposable
        {
            private readonly List<string> _createdDirectories = new();
            private readonly List<string> _createdFiles = new();
            private readonly string _baseTestDirectory;

            public TestEnvironmentFixture()
            {
                _baseTestDirectory = Path.Combine(Path.GetTempPath(), $"InputValidatorTest_{Guid.NewGuid():N}");
                Directory.CreateDirectory(_baseTestDirectory);
                _createdDirectories.Add(_baseTestDirectory);
            }

            public string CreateTestDirectory(string name)
            {
                var path = Path.Combine(_baseTestDirectory, name);
                Directory.CreateDirectory(path);
                _createdDirectories.Add(path);
                return path;
            }

            public string CreateTestFile(string fileName, string content = "# Test content")
            {
                var filePath = Path.Combine(_baseTestDirectory, fileName);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _createdDirectories.Add(directory);
                }
                File.WriteAllText(filePath, content);
                _createdFiles.Add(filePath);
                return filePath;
            }

            public string CreateTypeSpecDirectory(string dirName, string[] fileNames = null)
            {
                var dirPath = CreateTestDirectory(dirName);
                var files = fileNames ?? new[] { "main.tsp", "config.yaml" };
                
                foreach (var fileName in files)
                {
                    var filePath = Path.Combine(dirPath, fileName);
                    File.WriteAllText(filePath, "# TypeSpec content");
                    _createdFiles.Add(filePath);
                }

                return dirPath;
            }

            public string CreatePowerShellScript(string relativePath, string content = "# PowerShell script")
            {
                var fullPath = Path.Combine(_baseTestDirectory, relativePath);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _createdDirectories.Add(directory);
                }
                File.WriteAllText(fullPath, content);
                _createdFiles.Add(fullPath);
                return fullPath;
            }

            public void Dispose()
            {
                // Clean up files first
                foreach (var file in _createdFiles.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup failures
                    }
                }

                // Then clean up directories
                foreach (var directory in _createdDirectories.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (Directory.Exists(directory))
                        {
                            Directory.Delete(directory, true);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup failures
                    }
                }
            }
        }

        #endregion

        #region ValidateDirTraversal Tests

        [Test]
        public void ValidateDirTraversal_WithNullPath_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateDirTraversal(null));
            
            Assert.That(exception.ParamName, Is.EqualTo("path"));
            Assert.That(exception.Message, Does.Contain("path cannot be null or empty"));
        }

        [Test]
        public void ValidateDirTraversal_WithEmptyPath_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateDirTraversal(""));
            
            Assert.That(exception.ParamName, Is.EqualTo("path"));
            Assert.That(exception.Message, Does.Contain("path cannot be null or empty"));
        }

        [Test]
        public void ValidateDirTraversal_WithWhitespacePath_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateDirTraversal("   "));
            
            Assert.That(exception.ParamName, Is.EqualTo("path"));
            Assert.That(exception.Message, Does.Contain("path cannot be null or empty"));
        }

        [Test]
        public void ValidateDirTraversal_WithValidPath_ShouldReturnSuccess()
        {
            // Arrange
            var validPaths = new[]
            {
                @"C:\temp\typespec",
                "/home/user/project",
                "relative/path/to/project",
                "simple-name",
                "project_with_underscores",
                "project-with-dashes",
                "../relative/path"
            };

            // Act & Assert
            foreach (var path in validPaths)
            {
                var result = InputValidator.ValidateDirTraversal(path);

                Assert.That(result.IsSuccess, Is.True, $"Path '{path}' should be valid");
                Assert.That(result.Value, Is.EqualTo(path));
                Assert.That(result.Exception, Is.Null);
                Assert.That(result.ProcessException, Is.Null);
            }
        }

        [Test]
        public void ValidateDirTraversal_WithCustomPathType_ShouldReturnSuccess()
        {
            // Arrange
            var path = "test/path";
            
            // Act
            var result = InputValidator.ValidateDirTraversal(path, "Custom Path Type");

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(path));
        }

        [Test]
        public void ValidateDirTraversal_WithCustomPathType_WithNullPath_ShouldIncludeCustomPathType()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateDirTraversal(null, "Custom Path Type"));
            
            Assert.That(exception.Message, Does.Contain("Custom Path Type cannot be null or empty"));
        }

        #endregion

        #region ValidateTypeSpecDir Tests

        [Test]
        public void ValidateTypeSpecDir_WithNullPath_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateTypeSpecDir(null));
            
            Assert.That(exception.ParamName, Is.EqualTo("path"));
            Assert.That(exception.Message, Does.Contain("TypeSpec path cannot be null or empty"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithEmptyPath_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateTypeSpecDir(""));
            
            Assert.That(exception.ParamName, Is.EqualTo("path"));
            Assert.That(exception.Message, Does.Contain("TypeSpec path cannot be null or empty"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithValidLocalPath_ShouldReturnSuccess()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var typeSpecDir = fixture.CreateTypeSpecDirectory("ValidTypeSpec");

            // Act
            var result = InputValidator.ValidateTypeSpecDir(typeSpecDir, isLocalPath: true);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Path.GetFullPath(typeSpecDir)));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidateTypeSpecDir_WithNonExistentLocalPath_ShouldThrowDirectoryNotFoundException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var nonExistentPath = Path.Combine(fixture.CreateTestDirectory("temp"), "NonExistent");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateTypeSpecDir(nonExistentPath, isLocalPath: true));
            
            Assert.That(exception.InnerException, Is.TypeOf<DirectoryNotFoundException>());
            Assert.That(exception.Message, Does.Contain("Invalid path format"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithDirectoryContainingNoTypeSpecFiles_ShouldThrowInvalidOperationException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var emptyDir = fixture.CreateTestDirectory("EmptyDir");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateTypeSpecDir(emptyDir, isLocalPath: true));
            
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(exception.InnerException.Message, Does.Contain("No .tsp or .yaml files found"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithDirectoryContainingNonTypeSpecFiles_ShouldThrowInvalidOperationException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var dirWithMixedFiles = fixture.CreateTestDirectory("MixedFiles");
            File.WriteAllText(Path.Combine(dirWithMixedFiles, "valid.tsp"), "# TypeSpec");
            File.WriteAllText(Path.Combine(dirWithMixedFiles, "invalid.txt"), "# Text file");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateTypeSpecDir(dirWithMixedFiles, isLocalPath: true));
            
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(exception.InnerException.Message, Does.Contain("Directory contains non-TypeSpec files"));
            Assert.That(exception.InnerException.Message, Does.Contain("invalid.txt"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithValidRepositoryPath_ShouldNormalizePath()
        {
            // Arrange
            var repositoryPath = @"specification\storage\data-plane";

            // Act
            var result = InputValidator.ValidateTypeSpecDir(repositoryPath, isLocalPath: false);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("specification/storage/data-plane"));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidateTypeSpecDir_WithRepositoryPathStartingWithSlash_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateTypeSpecDir("/invalid/path", isLocalPath: false));
            
            Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(exception.InnerException.Message, Does.Contain("Repository path cannot start with / or \\"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithRepositoryPathStartingWithBackslash_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateTypeSpecDir(@"\invalid\path", isLocalPath: false));
            
            Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(exception.InnerException.Message, Does.Contain("Repository path cannot start with / or \\"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithRepositoryPathWithDoubleSeparators_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateTypeSpecDir("invalid//path", isLocalPath: false));
            
            Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(exception.InnerException.Message, Does.Contain("Repository path contains invalid double separators"));
        }

        [Test]
        public void ValidateTypeSpecDir_WithRepositoryPathWithDoubleBackslashes_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateTypeSpecDir(@"invalid\\path", isLocalPath: false));
            
            Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(exception.InnerException.Message, Does.Contain("Repository path contains invalid double separators"));
        }

        #endregion

        #region ValidateCommitId Tests

        [Test]
        public void ValidateCommitId_WithNullCommitId_ShouldReturnSuccessWithEmptyString()
        {
            // Act
            var result = InputValidator.ValidateCommitId(null);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(string.Empty));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidateCommitId_WithEmptyCommitId_ShouldReturnSuccessWithEmptyString()
        {
            // Act
            var result = InputValidator.ValidateCommitId("");

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(string.Empty));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidateCommitId_WithWhitespaceCommitId_ShouldReturnSuccessWithEmptyString()
        {
            // Act
            var result = InputValidator.ValidateCommitId("   ");

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(string.Empty));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidateCommitId_WithValidCommitIds_ShouldReturnSuccess()
        {
            // Arrange
            var validCommitIds = new[]
            {
                "abc123",                                  // 6 characters (minimum)
                "abc123def456",                           // 12 characters
                "abc123def456789012345678901234567890abcd", // 40 characters (maximum)
                "ABCDEF123456",                           // Uppercase
                "abcdef123456",                           // Lowercase
                "0123456789abcdef"                        // Mixed numbers and letters
            };

            // Act & Assert
            foreach (var commitId in validCommitIds)
            {
                Assert.DoesNotThrow(() => 
                {
                    var result = InputValidator.ValidateCommitId(commitId);
                    Assert.That(result.IsSuccess, Is.True, $"CommitId '{commitId}' should be valid");
                    Assert.That(result.Value, Is.EqualTo(commitId));
                    Assert.That(result.Exception, Is.Null);
                    Assert.That(result.ProcessException, Is.Null);
                });
            }
        }

        [Test]
        public void ValidateCommitId_WithInvalidCommitIds_ShouldThrowArgumentException()
        {
            // Arrange
            var invalidCommitIds = new[]
            {
                "abc12",                                     // Too short (5 characters)
                "abc123def456789012345678901234567890abcdefg", // Too long (41 characters)
                "abc123!",                                   // Contains special character
                "abc 123",                                   // Contains space
                "abc123g",                                   // Contains invalid hex character
                "zbc123"                                     // Contains invalid hex character
            };

            // Act & Assert
            foreach (var commitId in invalidCommitIds)
            {
                var exception = Assert.Throws<ArgumentException>(() => 
                    InputValidator.ValidateCommitId(commitId));

                Assert.That(exception.ParamName, Is.EqualTo("commitId"));
                Assert.That(exception.Message, Does.Contain("Commit ID must be 6-40 hexadecimal characters"));
            }
        }

        #endregion

        #region ValidateOutputDirectory Tests

        [Test]
        public void ValidateOutputDirectory_WithNullPath_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateOutputDirectory(null));
            
            Assert.That(exception.ParamName, Is.EqualTo("path"));
            Assert.That(exception.Message, Does.Contain("Output directory path cannot be null or empty"));
        }

        [Test]
        public void ValidateOutputDirectory_WithEmptyPath_ShouldThrowArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateOutputDirectory(""));
            
            Assert.That(exception.ParamName, Is.EqualTo("path"));
            Assert.That(exception.Message, Does.Contain("Output directory path cannot be null or empty"));
        }

        [Test]
        public void ValidateOutputDirectory_WithValidExistingDirectory_ShouldReturnSuccess()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var validDir = fixture.CreateTestDirectory("ValidOutput");

            // Act
            var result = InputValidator.ValidateOutputDirectory(validDir);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Path.GetFullPath(validDir)));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidateOutputDirectory_WithValidNonExistentDirectory_ShouldReturnSuccess()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange - Create parent directory but not the target directory
            var parentDir = fixture.CreateTestDirectory("Parent");
            var nonExistentPath = Path.Combine(parentDir, "NonExistentChild");

            // Act
            var result = InputValidator.ValidateOutputDirectory(nonExistentPath);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Path.GetFullPath(nonExistentPath)));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidateOutputDirectory_WithNonExistentParentDirectory_ShouldThrowDirectoryNotFoundException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var nonExistentParent = Path.Combine(fixture.CreateTestDirectory("temp"), "NonExistentParent", "child");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateOutputDirectory(nonExistentParent));
            
            Assert.That(exception.InnerException, Is.TypeOf<DirectoryNotFoundException>());
            Assert.That(exception.InnerException.Message, Does.Contain("Parent directory does not exist"));
        }

        #endregion

        #region ValidatePowerShellScriptPath Tests

        [Test]
        public void ValidatePowerShellScriptPath_WithNullScriptPath_ShouldThrowArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var azureSdkPath = fixture.CreateTestDirectory("AzureSDK");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidatePowerShellScriptPath(null, azureSdkPath));
            
            Assert.That(exception.ParamName, Is.EqualTo("scriptPath"));
            Assert.That(exception.Message, Does.Contain("PowerShell script path cannot be null or empty"));
        }

        [Test]
        public void ValidatePowerShellScriptPath_WithEmptyScriptPath_ShouldThrowArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var azureSdkPath = fixture.CreateTestDirectory("AzureSDK");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidatePowerShellScriptPath("", azureSdkPath));
            
            Assert.That(exception.ParamName, Is.EqualTo("scriptPath"));
            Assert.That(exception.Message, Does.Contain("PowerShell script path cannot be null or empty"));
        }

        [Test]
        public void ValidatePowerShellScriptPath_WithWhitespaceScriptPath_ShouldThrowArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var azureSdkPath = fixture.CreateTestDirectory("AzureSDK");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidatePowerShellScriptPath("   ", azureSdkPath));
            
            Assert.That(exception.ParamName, Is.EqualTo("scriptPath"));
            Assert.That(exception.Message, Does.Contain("PowerShell script path cannot be null or empty"));
        }

        [Test]
        public void ValidatePowerShellScriptPath_WithNonPs1Extension_ShouldThrowArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var azureSdkPath = fixture.CreateTestDirectory("AzureSDK");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidatePowerShellScriptPath("script.bat", azureSdkPath));
            
            Assert.That(exception.ParamName, Is.EqualTo("scriptPath"));
            Assert.That(exception.Message, Does.Contain("PowerShell script must have .ps1 extension"));
        }

        [Test]
        public void ValidatePowerShellScriptPath_WithValidScript_ShouldReturnSuccess()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var azureSdkPath = fixture.CreateTestDirectory("AzureSDK");
            var scriptRelativePath = @"eng\scripts\test.ps1";
            var scriptFullPath = Path.Combine(azureSdkPath, scriptRelativePath);
            var scriptDirectory = Path.GetDirectoryName(scriptFullPath);
            Directory.CreateDirectory(scriptDirectory!);
            File.WriteAllText(scriptFullPath, "# PowerShell script");

            // Act
            var result = InputValidator.ValidatePowerShellScriptPath(scriptRelativePath, azureSdkPath);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Path.Combine(azureSdkPath, scriptRelativePath)));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidatePowerShellScriptPath_WithNonExistentScript_ShouldThrowFileNotFoundException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var azureSdkPath = fixture.CreateTestDirectory("AzureSDK");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidatePowerShellScriptPath("nonexistent.ps1", azureSdkPath));
            
            Assert.That(exception.InnerException, Is.TypeOf<FileNotFoundException>());
            Assert.That(exception.InnerException.Message, Does.Contain("PowerShell script not found"));
        }

        [Test]
        public void ValidatePowerShellScriptPath_WithScriptOutsideAzureSDK_ShouldThrowUnauthorizedAccessException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var azureSdkPath = fixture.CreateTestDirectory("AzureSDK");
            var otherPath = fixture.CreateTestDirectory("Other");
            var externalScript = fixture.CreatePowerShellScript(Path.Combine(otherPath, "external.ps1"));
            
            // Try to use an absolute path outside the Azure SDK directory
            var relativePath = Path.GetRelativePath(azureSdkPath, externalScript);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidatePowerShellScriptPath(relativePath, azureSdkPath));
            
            // The script exists but is outside the Azure SDK directory, so should get UnauthorizedAccessException
            Assert.That(exception.InnerException, Is.TypeOf<UnauthorizedAccessException>());
        }

        [Test]
        public void ValidatePowerShellScriptPath_WithDirectoryTraversalAttempt_ShouldThrowFileNotFoundException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var azureSdkPath = fixture.CreateTestDirectory("AzureSDK");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidatePowerShellScriptPath(@"..\..\..\malicious.ps1", azureSdkPath));
            
            Assert.That(exception.InnerException, Is.TypeOf<FileNotFoundException>());
            Assert.That(exception.InnerException.Message, Does.Contain("PowerShell script not found"));
        }

        #endregion

        #region ValidateProcessArguments Tests

        [Test]
        public void ValidateProcessArguments_WithNullArguments_ShouldReturnSuccessWithEmptyString()
        {
            // Act
            var result = InputValidator.ValidateProcessArguments(null);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(string.Empty));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidateProcessArguments_WithEmptyArguments_ShouldReturnSuccessWithEmptyString()
        {
            // Act
            var result = InputValidator.ValidateProcessArguments("");

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(""));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidateProcessArguments_WithValidArguments_ShouldReturnSuccess()
        {
            // Arrange
            var validArguments = new[]
            {
                "install --global @typespec/compiler",
                "build --configuration Release",
                "-Command \"npm install\"",
                "compile . --emit @azure-tools/typespec-csharp",
                "single-word",
                "--flag value",
                "/parameter:value"
            };

            // Act & Assert
            foreach (var args in validArguments)
            {
                var result = InputValidator.ValidateProcessArguments(args);

                Assert.That(result.IsSuccess, Is.True, $"Arguments '{args}' should be valid");
                Assert.That(result.Value, Is.EqualTo(args));
                Assert.That(result.Exception, Is.Null);
                Assert.That(result.ProcessException, Is.Null);
            }
        }

        [Test]
        public void ValidateProcessArguments_WithDangerousCommandSeparators_ShouldThrowArgumentException()
        {
            // Arrange
            var dangerousArguments = new[]
            {
                "install && rm -rf /",
                "build | malicious-command",
                "install; shutdown -h now",
                "build || evil-command",
                "install & malicious"
            };

            // Act & Assert
            foreach (var args in dangerousArguments)
            {
                var exception = Assert.Throws<ArgumentException>(() => 
                    InputValidator.ValidateProcessArguments(args));

                Assert.That(exception.ParamName, Is.EqualTo("arguments"));
                Assert.That(exception.Message, Does.Contain("Arguments contain command separator:"));
            }
        }

        [Test]
        public void ValidateProcessArguments_WithIndividualCommandSeparators_ShouldThrowArgumentException()
        {
            // Arrange
            var commandSeparators = new[] { "&&", "||", "&", "|", ";" };

            // Act & Assert
            foreach (var separator in commandSeparators)
            {
                var testArg = $"safe-command {separator} other-arg";
                var exception = Assert.Throws<ArgumentException>(() => 
                    InputValidator.ValidateProcessArguments(testArg));

                Assert.That(exception.ParamName, Is.EqualTo("arguments"));
                Assert.That(exception.Message, Does.Contain($"Arguments contain command separator: {separator}"));
            }
        }

        [Test]
        public void ValidateProcessArguments_WithCaseInsensitiveDangerousPatterns_ShouldThrowArgumentException()
        {
            // Arrange
            var caseVariations = new[]
            {
                "install & malicious",
                "build | evil",
                "run ; dangerous"
            };

            // Act & Assert
            foreach (var args in caseVariations)
            {
                var exception = Assert.Throws<ArgumentException>(() => 
                    InputValidator.ValidateProcessArguments(args));

                Assert.That(exception.ParamName, Is.EqualTo("arguments"));
                Assert.That(exception.Message, Does.Contain("Arguments contain command separator:"));
            }
        }

        #endregion

        #region ValidateWorkingDirectory Tests

        [Test]
        public void ValidateWorkingDirectory_WithNullDirectory_ShouldReturnCurrentDirectory()
        {
            // Act
            var result = InputValidator.ValidateWorkingDirectory(null);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Directory.GetCurrentDirectory()));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidateWorkingDirectory_WithEmptyDirectory_ShouldReturnCurrentDirectory()
        {
            // Act
            var result = InputValidator.ValidateWorkingDirectory("");

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Directory.GetCurrentDirectory()));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidateWorkingDirectory_WithWhitespaceDirectory_ShouldReturnCurrentDirectory()
        {
            // Act
            var result = InputValidator.ValidateWorkingDirectory("   ");

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Directory.GetCurrentDirectory()));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidateWorkingDirectory_WithValidExistingDirectory_ShouldReturnSuccess()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var validDir = fixture.CreateTestDirectory("ValidWorkingDir");

            // Act
            var result = InputValidator.ValidateWorkingDirectory(validDir);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Path.GetFullPath(validDir)));
            Assert.That(result.Exception, Is.Null);
            Assert.That(result.ProcessException, Is.Null);
        }

        [Test]
        public void ValidateWorkingDirectory_WithNonExistentDirectory_ShouldThrowDirectoryNotFoundException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var nonExistentDir = Path.Combine(fixture.CreateTestDirectory("temp"), "NonExistentDir");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateWorkingDirectory(nonExistentDir));
            
            Assert.That(exception.InnerException, Is.TypeOf<DirectoryNotFoundException>());
            Assert.That(exception.InnerException.Message, Does.Contain("Working directory does not exist"));
        }

        [Test]
        public void ValidateWorkingDirectory_WithDirectoryTraversal_ShouldThrowDirectoryNotFoundException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                InputValidator.ValidateWorkingDirectory("../../../nonexistent"));
            
            Assert.That(exception.InnerException, Is.TypeOf<DirectoryNotFoundException>());
            Assert.That(exception.InnerException.Message, Does.Contain("Working directory does not exist"));
        }

        #endregion

        #region Edge Case and Integration Tests

        [Test]
        public void ValidateTypeSpecDir_WithMixedValidExtensions_ShouldReturnSuccess()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var typeSpecDir = fixture.CreateTypeSpecDirectory("MixedValid", new[] { "main.tsp", "config.yaml", "types.TSP", "settings.YAML" });

            // Act
            var result = InputValidator.ValidateTypeSpecDir(typeSpecDir, isLocalPath: true);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Path.GetFullPath(typeSpecDir)));
        }

        [Test]
        public void ValidateOutputDirectory_WithRootPath_ShouldReturnSuccess()
        {
            // Arrange
            var rootPath = Path.GetPathRoot(Directory.GetCurrentDirectory());

            // Act
            var result = InputValidator.ValidateOutputDirectory(rootPath);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Path.GetFullPath(rootPath)));
        }

        [Test]
        public void ValidateCommitId_WithEdgeCaseLengths_ShouldWork()
        {
            // Arrange & Act & Assert
            
            // Test exact minimum length (6)
            var result6 = InputValidator.ValidateCommitId("abc123");
            Assert.That(result6.IsSuccess, Is.True);
            Assert.That(result6.Value, Is.EqualTo("abc123"));

            // Test exact maximum length (40)
            var result40 = InputValidator.ValidateCommitId("abc123def456789012345678901234567890abcd");
            Assert.That(result40.IsSuccess, Is.True);
            Assert.That(result40.Value, Is.EqualTo("abc123def456789012345678901234567890abcd"));
        }

        [Test]
        public void ValidatePowerShellScriptPath_WithCaseInsensitiveExtension_ShouldReturnSuccess()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var azureSdkPath = fixture.CreateTestDirectory("AzureSDK");
            var scriptPath = "test.PS1"; // Uppercase extension
            var scriptFullPath = Path.Combine(azureSdkPath, scriptPath);
            File.WriteAllText(scriptFullPath, "# PowerShell script");

            // Act
            var result = InputValidator.ValidatePowerShellScriptPath(scriptPath, azureSdkPath);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(Path.Combine(azureSdkPath, scriptPath)));
        }

        [Test]
        public void ValidateProcessArguments_WithAllCommandSeparators_ShouldThrowForEachOne()
        {
            // Arrange
            var allSeparators = new[] { "&&", "||", "&", "|", ";" };

            // Act & Assert
            foreach (var separator in allSeparators)
            {
                var exception = Assert.Throws<ArgumentException>(() => 
                    InputValidator.ValidateProcessArguments($"command {separator} other"));

                Assert.That(exception.Message, Does.Contain($"Arguments contain command separator: {separator}"));
            }
        }

        #endregion

        #region Thread Safety and Parallel Execution Tests

        [Test]
        public void InputValidator_ConcurrentCallsToAllMethods_ShouldBeThreadSafe()
        {
            using var fixture = new TestEnvironmentFixture();
            
            // Arrange
            var typeSpecDir = fixture.CreateTypeSpecDirectory("ThreadSafeTest");
            var azureSdkDir = fixture.CreateTestDirectory("ThreadSafeAzureSDK");
            var scriptPath = Path.Combine(azureSdkDir, "test.ps1");
            File.WriteAllText(scriptPath, "# PowerShell script");
            var workingDir = fixture.CreateTestDirectory("ThreadSafeWorking");

            var tasks = new List<System.Threading.Tasks.Task>();

            // Act - Run multiple validation methods concurrently
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(System.Threading.Tasks.Task.Run(() =>
                {
                    Assert.DoesNotThrow(() => InputValidator.ValidateDirTraversal("test/path"));
                    Assert.DoesNotThrow(() => InputValidator.ValidateCommitId("abc123"));
                    Assert.DoesNotThrow(() => InputValidator.ValidateProcessArguments("safe command"));
                    Assert.DoesNotThrow(() => InputValidator.ValidateTypeSpecDir(typeSpecDir, true));
                    Assert.DoesNotThrow(() => InputValidator.ValidateOutputDirectory(typeSpecDir));
                    Assert.DoesNotThrow(() => InputValidator.ValidatePowerShellScriptPath("test.ps1", azureSdkDir));
                    Assert.DoesNotThrow(() => InputValidator.ValidateWorkingDirectory(workingDir));
                }));
            }

            // Assert
            Assert.DoesNotThrow(() => System.Threading.Tasks.Task.WaitAll(tasks.ToArray()));
        }

        #endregion
    }
}
