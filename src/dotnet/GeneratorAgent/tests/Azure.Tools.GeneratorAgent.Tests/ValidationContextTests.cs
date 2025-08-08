using NUnit.Framework;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Azure.Tools.GeneratorAgent.Security;
using System.IO;
using System;
using System.Collections.Generic;

namespace Azure.Tools.GeneratorAgent.Tests.Configuration
{
    [TestFixture]
    public class ValidationContextTests
    {
        private sealed class TestEnvironmentFixture : IDisposable
        {
            private readonly List<string> _createdDirectories = new();
            private readonly Mock<ILogger> _mockInputValidatorLogger;

            public TestEnvironmentFixture()
            {
                _mockInputValidatorLogger = new Mock<ILogger>();
                InputValidator.SetLogger(_mockInputValidatorLogger.Object);
            }

            public Mock<ILogger> CreateMockLogger()
            {
                return new Mock<ILogger>();
            }

            public string CreateTempDirectoryWithTypeSpecFiles(string baseName = "typespec-test")
            {
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var basePath = Path.Combine(Path.GetTempPath(), $"{baseName}-{uniqueId}");
                Directory.CreateDirectory(basePath);
                _createdDirectories.Add(basePath);
                
                File.WriteAllText(Path.Combine(basePath, "main.tsp"), "// Sample TypeSpec file");
                return basePath;
            }

            public string CreateTempDirectory(string baseName = "test-dir")
            {
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var basePath = Path.Combine(Path.GetTempPath(), $"{baseName}-{uniqueId}");
                Directory.CreateDirectory(basePath);
                _createdDirectories.Add(basePath);
                return basePath;
            }

            public string CreateEmptyTempDirectory(string baseName = "empty-test")
            {
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var basePath = Path.Combine(Path.GetTempPath(), $"{baseName}-{uniqueId}");
                Directory.CreateDirectory(basePath);
                _createdDirectories.Add(basePath);
                return basePath;
            }

            public string CreateValidTypeSpecPath() => "specs/data-plane";
            public string CreateValidCommitId() => "abc123def456789";
            public string CreateEmptyCommitId() => string.Empty;
            public string CreateInvalidTypeSpecPath() => "..\\..\\malicious\\path";
            public string CreateInvalidCommitId() => "invalid-commit-id!@#";
            public string CreateInvalidOutputPath() => "..\\..\\malicious\\path";

            public void Dispose()
            {
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
                        }
                    }
                }
            }
        }

        [Test]
        public void CreateFromValidatedInputs_WithValidInputs_ShouldCreateContext()
        {
            using var fixture = new TestEnvironmentFixture();
            
            const string typeSpecPath = @"C:\temp\typespec";
            const string commitId = "abc123";
            const string outputPath = @"C:\temp\output";

            var context = ValidationContext.CreateFromValidatedInputs(typeSpecPath, commitId, outputPath);

            Assert.That(context, Is.Not.Null);
            Assert.That(context.ValidatedTypeSpecDir, Is.EqualTo(typeSpecPath));
            Assert.That(context.ValidatedCommitId, Is.EqualTo(commitId));
            Assert.That(context.ValidatedSdkDir, Is.EqualTo(outputPath));
        }

        [Test]
        public void CreateFromValidatedInputs_WithEmptyStrings_ShouldCreateContext()
        {
            using var fixture = new TestEnvironmentFixture();
            
            const string typeSpecPath = "";
            const string commitId = "";
            const string outputPath = "";

            var context = ValidationContext.CreateFromValidatedInputs(typeSpecPath, commitId, outputPath);

            Assert.That(context, Is.Not.Null);
            Assert.That(context.ValidatedTypeSpecDir, Is.EqualTo(typeSpecPath));
            Assert.That(context.ValidatedCommitId, Is.EqualTo(commitId));
            Assert.That(context.ValidatedSdkDir, Is.EqualTo(outputPath));
        }

        [Test]
        public void CreateFromValidatedInputs_WithNullValues_ShouldCreateContext()
        {
            using var fixture = new TestEnvironmentFixture();
            
            string? typeSpecPath = null;
            string? commitId = null;
            string? outputPath = null;

            var context = ValidationContext.CreateFromValidatedInputs(typeSpecPath!, commitId!, outputPath!);

            Assert.That(context, Is.Not.Null);
            Assert.That(context.ValidatedTypeSpecDir, Is.Null);
            Assert.That(context.ValidatedCommitId, Is.Null);
            Assert.That(context.ValidatedSdkDir, Is.Null);
        }

        [Test]
        public void ValidateAndCreate_WithValidLocalPath_ShouldCreateContext()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var typeSpecPath = fixture.CreateTempDirectoryWithTypeSpecFiles("validation-test-typespec");
            var commitId = (string?)null;
            var outputPath = fixture.CreateTempDirectory("validation-test-output");
            var mockLogger = fixture.CreateMockLogger();

            var context = ValidationContext.ValidateAndCreate(typeSpecPath, commitId, outputPath, mockLogger.Object);

            Assert.That(context, Is.Not.Null);
            Assert.That(context.ValidatedTypeSpecDir, Is.EqualTo(Path.GetFullPath(typeSpecPath)));
            Assert.That(context.ValidatedCommitId, Is.EqualTo(string.Empty));
            Assert.That(context.ValidatedSdkDir, Is.EqualTo(Path.GetFullPath(outputPath)));
        }

        [Test]
        public void ValidateAndCreate_WithValidGitHubPath_ShouldCreateContext()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var typeSpecPath = fixture.CreateValidTypeSpecPath();
            var commitId = fixture.CreateValidCommitId();
            var outputPath = fixture.CreateTempDirectory("validation-test-github-output");
            var mockLogger = fixture.CreateMockLogger();

            var context = ValidationContext.ValidateAndCreate(typeSpecPath, commitId, outputPath, mockLogger.Object);

            Assert.That(context, Is.Not.Null);
            Assert.That(context.ValidatedTypeSpecDir, Is.EqualTo(typeSpecPath));
            Assert.That(context.ValidatedCommitId, Is.EqualTo(commitId));
            Assert.That(context.ValidatedSdkDir, Is.EqualTo(Path.GetFullPath(outputPath)));
        }

        [Test]
        public void ValidateAndCreate_WithInvalidTypeSpecPath_ShouldThrowArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var invalidTypeSpecPath = fixture.CreateInvalidTypeSpecPath();
            var commitId = (string?)null;
            var outputPath = fixture.CreateTempDirectory("validation-test-invalid-output");
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() =>
                ValidationContext.ValidateAndCreate(invalidTypeSpecPath, commitId, outputPath, mockLogger.Object));

            Assert.That(ex!.ParamName, Is.EqualTo("typespecPath"));
            Assert.That(ex.Message, Does.Contain("TypeSpec path validation failed"));
        }

        [Test]
        public void ValidateAndCreate_WithInvalidCommitId_ShouldThrowArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var typeSpecPath = fixture.CreateValidTypeSpecPath();
            var invalidCommitId = fixture.CreateInvalidCommitId();
            var outputPath = fixture.CreateTempDirectory("validation-test-commit-output");
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() =>
                ValidationContext.ValidateAndCreate(typeSpecPath, invalidCommitId, outputPath, mockLogger.Object));

            Assert.That(ex!.ParamName, Is.EqualTo("commitId"));
            Assert.That(ex.Message, Does.Contain("Commit ID validation failed"));
        }

        [Test]
        public void ValidateAndCreate_WithInvalidOutputPath_ShouldThrowArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var typeSpecPath = fixture.CreateValidTypeSpecPath();
            var commitId = fixture.CreateValidCommitId();
            var invalidOutputPath = fixture.CreateInvalidOutputPath();
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() =>
                ValidationContext.ValidateAndCreate(typeSpecPath, commitId, invalidOutputPath, mockLogger.Object));

            Assert.That(ex!.ParamName, Is.EqualTo("sdkOutputPath"));
            Assert.That(ex.Message, Does.Contain("SDK output path validation failed"));
        }

        [Test]
        public void ValidateAndCreate_ShouldLogValidationSteps()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var typeSpecPath = fixture.CreateValidTypeSpecPath();
            var commitId = fixture.CreateValidCommitId();
            var outputPath = fixture.CreateTempDirectory("validation-test-logging-output");
            var mockLogger = fixture.CreateMockLogger();

            ValidationContext.ValidateAndCreate(typeSpecPath, commitId, outputPath, mockLogger.Object);

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting input validation")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

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
        public void ValidateAndCreate_WithNullLogger_ShouldThrowArgumentNullException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var typeSpecPath = fixture.CreateValidTypeSpecPath();
            var commitId = fixture.CreateValidCommitId();
            var outputPath = fixture.CreateTempDirectory("validation-test-null-logger-output");

            Assert.Throws<ArgumentNullException>(() =>
                ValidationContext.ValidateAndCreate(typeSpecPath, commitId, outputPath, null!));
        }

        [Test]
        public void ValidateAndCreate_WithMissingTypeSpecFiles_ShouldThrowArgumentException()
        {
            using var fixture = new TestEnvironmentFixture();
            
            var emptyTypeSpecPath = fixture.CreateEmptyTempDirectory("validation-test-empty-typespec");
            var commitId = (string?)null;
            var outputPath = fixture.CreateTempDirectory("validation-test-empty-output");
            var mockLogger = fixture.CreateMockLogger();

            var ex = Assert.Throws<ArgumentException>(() =>
                ValidationContext.ValidateAndCreate(emptyTypeSpecPath, commitId, outputPath, mockLogger.Object));

            Assert.That(ex!.ParamName, Is.EqualTo("typespecPath"));
            Assert.That(ex.Message, Does.Contain("No .tsp or .yaml files found"));
        }
    }
}
