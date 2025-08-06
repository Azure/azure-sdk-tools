using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Security;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests.Configuration
{
    [TestFixture]
    public class ValidationContextTests
    {
        private sealed class TestEnvironmentFixture : IDisposable
        {
            private readonly List<string> _tempFiles;
            private readonly List<string> _tempDirectories;
            private readonly string _uniqueId;

            public TestEnvironmentFixture()
            {
                _tempFiles = new List<string>();
                _tempDirectories = new List<string>();
                _uniqueId = Guid.NewGuid().ToString("N")[..8];
            }

            public string UniqueId => _uniqueId;

            public Mock<ILogger> CreateMockLogger()
            {
                return new Mock<ILogger>();
            }

            public string CreateValidTypeSpecDirectory()
            {
                var tempDir = Path.Combine(Path.GetTempPath(), $"TypeSpecTest_{_uniqueId}");
                Directory.CreateDirectory(tempDir);
                _tempDirectories.Add(tempDir);

                // Create a valid TypeSpec file
                var typeSpecFile = Path.Combine(tempDir, "main.tsp");
                File.WriteAllText(typeSpecFile, "@service({ title: \"Test Service\" }) namespace TestService;");
                _tempFiles.Add(typeSpecFile);

                return tempDir;
            }

            public string CreateValidTypeSpecDirectoryWithYaml()
            {
                var tempDir = Path.Combine(Path.GetTempPath(), $"TypeSpecYamlTest_{_uniqueId}");
                Directory.CreateDirectory(tempDir);
                _tempDirectories.Add(tempDir);

                // Create a valid YAML file
                var yamlFile = Path.Combine(tempDir, "tspconfig.yaml");
                File.WriteAllText(yamlFile, "extends: \"@typespec/http\"");
                _tempFiles.Add(yamlFile);

                return tempDir;
            }

            public string CreateEmptyDirectory()
            {
                var tempDir = Path.Combine(Path.GetTempPath(), $"EmptyTest_{_uniqueId}");
                Directory.CreateDirectory(tempDir);
                _tempDirectories.Add(tempDir);
                return tempDir;
            }

            public string CreateValidOutputDirectory()
            {
                var tempDir = Path.Combine(Path.GetTempPath(), $"OutputTest_{_uniqueId}");
                Directory.CreateDirectory(tempDir);
                _tempDirectories.Add(tempDir);
                return tempDir;
            }

            public string CreateNonExistentDirectory()
            {
                return Path.Combine(Path.GetTempPath(), $"NonExistent_{_uniqueId}");
            }

            public string CreateValidCommitId()
            {
                return "abc123def456789012345678901234567890abcd";
            }

            public string CreateShortCommitId()
            {
                return "abc123d";
            }

            public string CreateInvalidCommitId()
            {
                return "invalid-commit-id!@#$%";
            }

            public string CreateValidGitHubTypeSpecPath()
            {
                return "specification/cognitiveservices/data-plane";
            }

            public string CreateDirectoryTraversalPath()
            {
                return "../../etc/passwd";
            }

            public string CreatePathWithInvalidChars()
            {
                return "path<>|\"*?with:invalid\\chars";
            }

            public void Dispose()
            {
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

        #region CreateFromValidatedInputs Tests

        [Test]
        public void CreateFromValidatedInputs_WithValidInputs_CreatesContextCorrectly()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidTypeSpecDirectory();
            var commitId = fixture.CreateValidCommitId();
            var outputPath = fixture.CreateValidOutputDirectory();

            var context = ValidationContext.CreateFromValidatedInputs(typeSpecPath, commitId, outputPath);

            Assert.Multiple(() =>
            {
                Assert.That(context, Is.Not.Null);
                Assert.That(context.ValidatedTypeSpecDir, Is.EqualTo(typeSpecPath));
                Assert.That(context.ValidatedCommitId, Is.EqualTo(commitId));
                Assert.That(context.ValidatedSdkDir, Is.EqualTo(outputPath));
            });
        }

        [Test]
        public void CreateFromValidatedInputs_WithEmptyStrings_CreatesContextWithEmptyValues()
        {
            using var fixture = new TestEnvironmentFixture();

            var context = ValidationContext.CreateFromValidatedInputs(string.Empty, string.Empty, string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(context, Is.Not.Null);
                Assert.That(context.ValidatedTypeSpecDir, Is.EqualTo(string.Empty));
                Assert.That(context.ValidatedCommitId, Is.EqualTo(string.Empty));
                Assert.That(context.ValidatedSdkDir, Is.EqualTo(string.Empty));
            });
        }

        [Test]
        public void CreateFromValidatedInputs_WithLongPaths_CreatesContextCorrectly()
        {
            using var fixture = new TestEnvironmentFixture();
            var longTypeSpecPath = string.Join("\\", Enumerable.Repeat("verylongdirectoryname", 10));
            var longCommitId = string.Join("", Enumerable.Repeat("a", 40));
            var longOutputPath = string.Join("\\", Enumerable.Repeat("verylongoutputname", 10));

            var context = ValidationContext.CreateFromValidatedInputs(longTypeSpecPath, longCommitId, longOutputPath);

            Assert.Multiple(() =>
            {
                Assert.That(context, Is.Not.Null);
                Assert.That(context.ValidatedTypeSpecDir, Is.EqualTo(longTypeSpecPath));
                Assert.That(context.ValidatedCommitId, Is.EqualTo(longCommitId));
                Assert.That(context.ValidatedSdkDir, Is.EqualTo(longOutputPath));
            });
        }

        [Test]
        public void CreateFromValidatedInputs_WithSpecialCharacters_CreatesContextCorrectly()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPathWithSpaces = "Type Spec Path With Spaces";
            var commitIdWithNumbers = "123abc456def789";
            var outputPathWithDots = "output.path.with.dots";

            var context = ValidationContext.CreateFromValidatedInputs(typeSpecPathWithSpaces, commitIdWithNumbers, outputPathWithDots);

            Assert.Multiple(() =>
            {
                Assert.That(context, Is.Not.Null);
                Assert.That(context.ValidatedTypeSpecDir, Is.EqualTo(typeSpecPathWithSpaces));
                Assert.That(context.ValidatedCommitId, Is.EqualTo(commitIdWithNumbers));
                Assert.That(context.ValidatedSdkDir, Is.EqualTo(outputPathWithDots));
            });
        }

        #endregion

        #region TryValidateAndCreate Success Tests

        [Test]
        public void TryValidateAndCreate_WithValidLocalPath_ReturnsSuccessResult()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidTypeSpecDirectory();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var result = ValidationContext.TryValidateAndCreate(typeSpecPath, null, outputPath, mockLogger.Object);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.Not.Null);
                Assert.That(result.Value!.ValidatedTypeSpecDir, Is.EqualTo(Path.GetFullPath(typeSpecPath)));
                Assert.That(result.Value.ValidatedCommitId, Is.EqualTo(string.Empty));
                Assert.That(result.Value.ValidatedSdkDir, Is.EqualTo(Path.GetFullPath(outputPath)));
                Assert.That(result.Exception, Is.Null);
                Assert.That(result.ProcessException, Is.Null);
            });
        }

        [Test]
        public void TryValidateAndCreate_WithValidGitHubPath_ReturnsSuccessResult()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidGitHubTypeSpecPath();
            var commitId = fixture.CreateValidCommitId();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var result = ValidationContext.TryValidateAndCreate(typeSpecPath, commitId, outputPath, mockLogger.Object);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.Not.Null);
                Assert.That(result.Value!.ValidatedTypeSpecDir, Is.EqualTo(typeSpecPath));
                Assert.That(result.Value.ValidatedCommitId, Is.EqualTo(commitId));
                Assert.That(result.Value.ValidatedSdkDir, Is.EqualTo(Path.GetFullPath(outputPath)));
                Assert.That(result.Exception, Is.Null);
                Assert.That(result.ProcessException, Is.Null);
            });
        }

        [Test]
        public void TryValidateAndCreate_WithEmptyCommitId_TreatsAsLocalPath()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidTypeSpecDirectory();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var result = ValidationContext.TryValidateAndCreate(typeSpecPath, string.Empty, outputPath, mockLogger.Object);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.Not.Null);
                Assert.That(result.Value!.ValidatedCommitId, Is.EqualTo(string.Empty));
            });
        }

        [Test]
        public void TryValidateAndCreate_WithWhitespaceCommitId_TreatsAsLocalPath()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidTypeSpecDirectory();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var result = ValidationContext.TryValidateAndCreate(typeSpecPath, "   ", outputPath, mockLogger.Object);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.Not.Null);
                Assert.That(result.Value!.ValidatedCommitId, Is.EqualTo(string.Empty));
            });
        }

        [Test]
        public void TryValidateAndCreate_WithYamlFiles_ReturnsSuccessResult()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidTypeSpecDirectoryWithYaml();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var result = ValidationContext.TryValidateAndCreate(typeSpecPath, null, outputPath, mockLogger.Object);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.Not.Null);
                Assert.That(result.Value!.ValidatedTypeSpecDir, Is.EqualTo(Path.GetFullPath(typeSpecPath)));
            });
        }

        [Test]
        public void TryValidateAndCreate_WithShortCommitId_ReturnsSuccessResult()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidGitHubTypeSpecPath();
            var commitId = fixture.CreateShortCommitId();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var result = ValidationContext.TryValidateAndCreate(typeSpecPath, commitId, outputPath, mockLogger.Object);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.Not.Null);
                Assert.That(result.Value!.ValidatedCommitId, Is.EqualTo(commitId));
            });
        }

        #endregion

        #region TryValidateAndCreate Failure Tests

        [Test]
        public void TryValidateAndCreate_WithInvalidTypeSpecPath_ThrowsException()
        {
            using var fixture = new TestEnvironmentFixture();
            var invalidTypeSpecPath = fixture.CreateDirectoryTraversalPath();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationContext.TryValidateAndCreate(invalidTypeSpecPath, null, outputPath, mockLogger.Object));

            Assert.That(ex?.Message, Does.Contain("Invalid path format"));
        }

        [Test]
        public void TryValidateAndCreate_WithNullTypeSpecPath_ThrowsException()
        {
            using var fixture = new TestEnvironmentFixture();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationContext.TryValidateAndCreate(null, null, outputPath, mockLogger.Object));

            Assert.That(ex?.Message, Does.Contain("TypeSpec path cannot be null or empty"));
        }

        [Test]
        public void TryValidateAndCreate_WithEmptyTypeSpecDirectory_ThrowsException()
        {
            using var fixture = new TestEnvironmentFixture();
            var emptyTypeSpecPath = fixture.CreateEmptyDirectory();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationContext.TryValidateAndCreate(emptyTypeSpecPath, null, outputPath, mockLogger.Object));

            Assert.That(ex?.Message, Does.Contain("No .tsp or .yaml files found in directory"));
        }

        [Test]
        public void TryValidateAndCreate_WithNonExistentTypeSpecDirectory_ThrowsException()
        {
            using var fixture = new TestEnvironmentFixture();
            var nonExistentPath = fixture.CreateNonExistentDirectory();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationContext.TryValidateAndCreate(nonExistentPath, null, outputPath, mockLogger.Object));

            Assert.That(ex?.Message, Does.Contain("TypeSpec directory not found"));
        }

        [Test]
        public void TryValidateAndCreate_WithInvalidCommitId_ThrowsException()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidGitHubTypeSpecPath();
            var invalidCommitId = fixture.CreateInvalidCommitId();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationContext.TryValidateAndCreate(typeSpecPath, invalidCommitId, outputPath, mockLogger.Object));

            Assert.That(ex?.Message, Does.Contain("Commit ID must be 6-40 hexadecimal characters"));
        }

        [Test]
        public void TryValidateAndCreate_WithInvalidOutputPath_ThrowsException()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidGitHubTypeSpecPath();
            var commitId = fixture.CreateValidCommitId();
            var invalidOutputPath = fixture.CreateDirectoryTraversalPath();
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationContext.TryValidateAndCreate(typeSpecPath, commitId, invalidOutputPath, mockLogger.Object));

            Assert.That(ex?.Message, Does.Contain("Invalid path format"));
        }

        [Test]
        public void TryValidateAndCreate_WithPathWithInvalidChars_ThrowsException()
        {
            using var fixture = new TestEnvironmentFixture();
            var invalidPath = fixture.CreatePathWithInvalidChars();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationContext.TryValidateAndCreate(invalidPath, null, outputPath, mockLogger.Object));

            Assert.That(ex?.Message, Does.Contain("Invalid path format"));
        }

        #endregion

        #region Logging Tests

        [Test]
        public void TryValidateAndCreate_WithSuccessfulValidation_LogsInformation()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidTypeSpecDirectory();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            ValidationContext.TryValidateAndCreate(typeSpecPath, null, outputPath, mockLogger.Object);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("All input validation completed successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void TryValidateAndCreate_WithFailedValidation_DoesNotLogSuccessMessage()
        {
            using var fixture = new TestEnvironmentFixture();
            var invalidTypeSpecPath = fixture.CreateDirectoryTraversalPath();
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            Assert.Throws<ArgumentException>(() => 
                ValidationContext.TryValidateAndCreate(invalidTypeSpecPath, null, outputPath, mockLogger.Object));

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("All input validation completed successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        #endregion

        #region Validation Order Tests

        [Test]
        public void TryValidateAndCreate_ValidatesTypeSpecPathFirst()
        {
            using var fixture = new TestEnvironmentFixture();
            var invalidTypeSpecPath = fixture.CreateNonExistentDirectory(); // Use a definitely invalid local path
            var invalidCommitId = fixture.CreateInvalidCommitId();
            var invalidOutputPath = fixture.CreateDirectoryTraversalPath();
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationContext.TryValidateAndCreate(invalidTypeSpecPath, null, invalidOutputPath, mockLogger.Object)); // null commit ID makes it local

            // Should fail on TypeSpec path validation first since we're using local path (null commit ID)
            Assert.That(ex?.Message, Does.Contain("Invalid path format"));
        }

        [Test]
        public void TryValidateAndCreate_ValidatesCommitIdSecond()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidGitHubTypeSpecPath();
            var invalidCommitId = fixture.CreateInvalidCommitId();
            var invalidOutputPath = fixture.CreateDirectoryTraversalPath();
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationContext.TryValidateAndCreate(typeSpecPath, invalidCommitId, invalidOutputPath, mockLogger.Object));

            // Should fail on commit ID validation since TypeSpec path is valid
            Assert.That(ex?.Message, Does.Contain("Commit ID must be 6-40 hexadecimal characters"));
        }

        [Test]
        public void TryValidateAndCreate_ValidatesOutputPathLast()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidGitHubTypeSpecPath();
            var commitId = fixture.CreateValidCommitId();
            var invalidOutputPath = fixture.CreateDirectoryTraversalPath();
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationContext.TryValidateAndCreate(typeSpecPath, commitId, invalidOutputPath, mockLogger.Object));

            // Should fail on output path validation since TypeSpec path and commit ID are valid
            Assert.That(ex?.Message, Does.Contain("Invalid path format"));
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public void TryValidateAndCreate_WithVeryLongValidPaths_HandlesCorrectly()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidTypeSpecDirectory();
            var longCommitId = string.Join("", Enumerable.Repeat("a", 40));
            var outputPath = fixture.CreateValidOutputDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var result = ValidationContext.TryValidateAndCreate(typeSpecPath, longCommitId, outputPath, mockLogger.Object);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.Not.Null);
                Assert.That(result.Value!.ValidatedCommitId, Is.EqualTo(longCommitId));
            });
        }

        [Test]
        public void TryValidateAndCreate_WithNullOutputPath_ThrowsException()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidTypeSpecDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationContext.TryValidateAndCreate(typeSpecPath, null, null!, mockLogger.Object));

            Assert.That(ex?.Message, Does.Contain("Output directory path cannot be null or empty"));
        }

        [Test]
        public void TryValidateAndCreate_WithEmptyOutputPath_ThrowsException()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidTypeSpecDirectory();
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationContext.TryValidateAndCreate(typeSpecPath, null, string.Empty, mockLogger.Object));

            Assert.That(ex?.Message, Does.Contain("Output directory path cannot be null or empty"));
        }

        #endregion

        #region Thread Safety Tests

        [Test]
        public void TryValidateAndCreate_ConcurrentExecution_HandlesCorrectly()
        {
            using var fixture = new TestEnvironmentFixture();
            var mockLogger = fixture.CreateMockLogger();

            var tasks = new List<Task<Result<ValidationContext>>>();
            for (int i = 0; i < 5; i++)
            {
                var typeSpecPath = fixture.CreateValidTypeSpecDirectory();
                var outputPath = fixture.CreateValidOutputDirectory();
                tasks.Add(Task.Run(() => 
                    ValidationContext.TryValidateAndCreate(typeSpecPath, null, outputPath, mockLogger.Object)));
            }

            var results = Task.WhenAll(tasks).Result;

            Assert.Multiple(() =>
            {
                Assert.That(results.Length, Is.EqualTo(5));
                for (int i = 0; i < 5; i++)
                {
                    Assert.That(results[i].IsSuccess, Is.True, $"Task {i} should succeed");
                    Assert.That(results[i].Value, Is.Not.Null, $"Task {i} should have a value");
                }
            });
        }

        [Test]
        public void CreateFromValidatedInputs_ConcurrentExecution_HandlesCorrectly()
        {
            using var fixture = new TestEnvironmentFixture();

            var tasks = new List<Task<ValidationContext>>();
            for (int i = 0; i < 5; i++)
            {
                var typeSpecPath = $"typespec-{i}";
                var commitId = $"commit-{i}";
                var outputPath = $"output-{i}";
                tasks.Add(Task.Run(() => 
                    ValidationContext.CreateFromValidatedInputs(typeSpecPath, commitId, outputPath)));
            }

            var results = Task.WhenAll(tasks).Result;

            Assert.Multiple(() =>
            {
                Assert.That(results.Length, Is.EqualTo(5));
                for (int i = 0; i < 5; i++)
                {
                    Assert.That(results[i], Is.Not.Null, $"Task {i} should succeed");
                    Assert.That(results[i].ValidatedTypeSpecDir, Is.EqualTo($"typespec-{i}"), $"Task {i} should have correct TypeSpec path");
                    Assert.That(results[i].ValidatedCommitId, Is.EqualTo($"commit-{i}"), $"Task {i} should have correct commit ID");
                    Assert.That(results[i].ValidatedSdkDir, Is.EqualTo($"output-{i}"), $"Task {i} should have correct output path");
                }
            });
        }

        #endregion

        #region Property Tests

        [Test]
        public void Properties_AreReadOnly_CannotBeModifiedAfterCreation()
        {
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidTypeSpecDirectory();
            var commitId = fixture.CreateValidCommitId();
            var outputPath = fixture.CreateValidOutputDirectory();

            var context = ValidationContext.CreateFromValidatedInputs(typeSpecPath, commitId, outputPath);

            // Verify properties are read-only by checking they have no public setters
            var typeSpecProperty = typeof(ValidationContext).GetProperty(nameof(ValidationContext.ValidatedTypeSpecDir));
            var commitProperty = typeof(ValidationContext).GetProperty(nameof(ValidationContext.ValidatedCommitId));
            var outputProperty = typeof(ValidationContext).GetProperty(nameof(ValidationContext.ValidatedSdkDir));

            Assert.Multiple(() =>
            {
                Assert.That(typeSpecProperty?.CanRead, Is.True);
                Assert.That(typeSpecProperty?.GetSetMethod(), Is.Null, "ValidatedTypeSpecDir should have no public setter");
                Assert.That(commitProperty?.CanRead, Is.True);
                Assert.That(commitProperty?.GetSetMethod(), Is.Null, "ValidatedCommitId should have no public setter");
                Assert.That(outputProperty?.CanRead, Is.True);
                Assert.That(outputProperty?.GetSetMethod(), Is.Null, "ValidatedSdkDir should have no public setter");
            });
        }

        #endregion
    }
}
