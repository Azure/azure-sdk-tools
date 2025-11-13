using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Security;
using System.Net.Http;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class TypeSpecFileServiceTests
    {
        private static Mock<ILogger<TypeSpecFileService>> CreateMockLogger()
        {
            return new Mock<ILogger<TypeSpecFileService>>();
        }

        private static AppSettings CreateAppSettings()
        {
            var configMock = new Mock<IConfiguration>();
            var loggerMock = new Mock<ILogger<AppSettings>>();
            return new AppSettings(configMock.Object, loggerMock.Object);
        }

        private static ValidationContext CreateLocalValidationContext(string typeSpecDir, string outputDir)
        {
            return ValidationContext.ValidateAndCreate(typeSpecDir, null, outputDir);
        }

        private static ValidationContext CreateGitHubValidationContext(string typeSpecPath, string commitId, string outputDir)
        {
            return ValidationContext.ValidateAndCreate(typeSpecPath, commitId, outputDir);
        }

        private static TypeSpecFileService CreateService(
            Mock<ILogger<TypeSpecFileService>>? mockLogger = null,
            GitHubFileService? gitHubFileService = null)
        {
            return new TypeSpecFileService(
                (mockLogger ?? CreateMockLogger()).Object,
                gitHubFileService ?? CreateGitHubFileService());
        }

        private static GitHubFileService CreateGitHubFileService()
        {
            var appSettings = CreateAppSettings();
            var logger = new Mock<ILogger<GitHubFileService>>().Object;
            var httpClient = new HttpClient();
            return new GitHubFileService(appSettings, logger, httpClient);
        }

        private static Mock<GitHubFileService> CreateMockGitHubService()
        {
            var mock = new Mock<GitHubFileService>();
            // Note: Cannot mock concrete GitHubFileService directly, this is for illustration
            return mock;
        }

        private sealed class TestEnvironmentFixture : IDisposable
        {
            private readonly string _tempDirectory;
            private bool _disposed;

            public string TempDirectory => _tempDirectory;

            public TestEnvironmentFixture()
            {
                _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(_tempDirectory);
            }

            public string CreateValidTypeSpecDirectory()
            {
                var typeSpecDir = Path.Combine(_tempDirectory, "typespec");
                Directory.CreateDirectory(typeSpecDir);
                
                // Create a valid TypeSpec file
                var typeSpecFile = Path.Combine(typeSpecDir, "main.tsp");
                File.WriteAllText(typeSpecFile, "// Valid TypeSpec content\n");
                
                return typeSpecDir;
            }

            public string CreateValidOutputDirectory()
            {
                var outputDir = Path.Combine(_tempDirectory, "output");
                Directory.CreateDirectory(outputDir);
                return outputDir;
            }

            public string CreateValidGitHubTypeSpecPath()
            {
                return "specification/typespec";
            }

            public string CreateValidCommitId()
            {
                return "abc123def456";
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    try
                    {
                        if (Directory.Exists(_tempDirectory))
                        {
                            Directory.Delete(_tempDirectory, true);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors in tests
                    }
                    _disposed = true;
                }
            }
        }

        [Test]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange
            var mockLogger = CreateMockLogger();
            var gitHubFileService = CreateGitHubFileService();

            // Act
            var service = CreateService(mockLogger, gitHubFileService);

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange
            var gitHubFileService = CreateGitHubFileService();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new TypeSpecFileService(null!, gitHubFileService));
            Assert.That(exception!.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullGitHubFileService_ShouldThrowArgumentNullException()
        {
            // Arrange
            var mockLogger = CreateMockLogger();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new TypeSpecFileService(mockLogger.Object, null!));
            Assert.That(exception!.ParamName, Is.EqualTo("gitHubFileService"));
        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithValidLocalPath_ShouldReturnFiles()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act
            var result = await service.GetTypeSpecFilesAsync(validationContext, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Count, Is.GreaterThan(0));
                Assert.That(result.ContainsKey("main.tsp"), Is.True);
                Assert.That(result["main.tsp"], Does.Contain("Valid TypeSpec content"));
            });
        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithMultipleFiles_ShouldReturnAllFiles()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            
            File.WriteAllText(Path.Combine(typeSpecDir, "second.tsp"), "// Second file content");
            File.WriteAllText(Path.Combine(typeSpecDir, "third.tsp"), "// Third file content");
            
            var outputDir = fixture.CreateValidOutputDirectory();
            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act
            var result = await service.GetTypeSpecFilesAsync(validationContext, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.Count, Is.EqualTo(3));
                Assert.That(result.ContainsKey("main.tsp"), Is.True);
                Assert.That(result.ContainsKey("second.tsp"), Is.True);
                Assert.That(result.ContainsKey("third.tsp"), Is.True);
            });
        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithSubDirectories_ShouldReturnAllFiles()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            
            var subDir = Path.Combine(typeSpecDir, "subdir");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "subfile.tsp"), "// Sub file content");
            
            var outputDir = fixture.CreateValidOutputDirectory();
            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act
            var result = await service.GetTypeSpecFilesAsync(validationContext, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.Count, Is.EqualTo(2));
                Assert.That(result.ContainsKey("main.tsp"), Is.True);
                Assert.That(result.ContainsKey("subfile.tsp"), Is.True);
            });
        }

        [Test]
        public void GetTypeSpecFilesAsync_WithNullValidationContext_ShouldThrowArgumentNullException()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentNullException>(() => 
                service.GetTypeSpecFilesAsync(null!, CancellationToken.None));
            Assert.That(exception!.ParamName, Is.EqualTo("validationContext"));
        }

        [Test]
        public void GetTypeSpecFilesAsync_WithCancellation_ShouldThrowInvalidOperationException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert - Service wraps TaskCanceledException in InvalidOperationException
            Assert.ThrowsAsync<InvalidOperationException>(() => 
                service.GetTypeSpecFilesAsync(validationContext, cts.Token));
        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithLargeFiles_ShouldHandleCorrectly()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            
            // Create a large file
            var largeContent = new string('a', 100000); // 100KB content
            File.WriteAllText(Path.Combine(typeSpecDir, "large.tsp"), largeContent);
            
            var outputDir = fixture.CreateValidOutputDirectory();
            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act
            var result = await service.GetTypeSpecFilesAsync(validationContext, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ContainsKey("large.tsp"), Is.True);
                Assert.That(result["large.tsp"].Length, Is.EqualTo(100000));
            });
        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithSpecialCharactersInContent_ShouldHandleCorrectly()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            
            var specialContent = "// Special chars: äöü 中文 🚀 \n\r\t";
            File.WriteAllText(Path.Combine(typeSpecDir, "special.tsp"), specialContent, System.Text.Encoding.UTF8);
            
            var outputDir = fixture.CreateValidOutputDirectory();
            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act
            var result = await service.GetTypeSpecFilesAsync(validationContext, CancellationToken.None);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.ContainsKey("special.tsp"), Is.True);
                Assert.That(result["special.tsp"], Is.EqualTo(specialContent));
            });
        }

        [Test]
        public async Task UpdateTypeSpecFileAsync_WithValidParameters_ShouldUpdateFile()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            var fileName = "updated.tsp";
            var content = "// Updated content";

            // Act
            var result = await service.UpdateTypeSpecFileAsync(fileName, content, validationContext, CancellationToken.None);

            // Assert
            var filePath = Path.Combine(typeSpecDir, fileName);
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(File.Exists(filePath), Is.True);
                Assert.That(File.ReadAllText(filePath), Is.EqualTo(content));
            });
        }

        [Test]
        public async Task UpdateTypeSpecFileAsync_OverwriteExistingFile_ShouldUpdateContent()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            var fileName = "main.tsp";
            var newContent = "// Updated main content";

            // Act
            var result = await service.UpdateTypeSpecFileAsync(fileName, newContent, validationContext, CancellationToken.None);

            // Assert
            var filePath = Path.Combine(typeSpecDir, fileName);
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(File.ReadAllText(filePath), Is.EqualTo(newContent));
            });
        }

        [Test]
        public void UpdateTypeSpecFileAsync_WithNullFileName_ShouldThrowArgumentNullException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentNullException>(() => 
                service.UpdateTypeSpecFileAsync(null!, "content", validationContext, CancellationToken.None));
            Assert.That(exception!.ParamName, Is.EqualTo("fileName"));
        }

        [Test]
        public void UpdateTypeSpecFileAsync_WithEmptyFileName_ShouldThrowArgumentException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                service.UpdateTypeSpecFileAsync("", "content", validationContext, CancellationToken.None));
            Assert.That(exception!.Message, Does.Contain("fileName"));
        }

        [Test]
        public void UpdateTypeSpecFileAsync_WithWhitespaceFileName_ShouldThrowArgumentException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentException>(() => 
                service.UpdateTypeSpecFileAsync("   ", "content", validationContext, CancellationToken.None));
            Assert.That(exception!.Message, Does.Contain("fileName"));
        }

        [Test]
        public void UpdateTypeSpecFileAsync_WithNullContent_ShouldThrowArgumentNullException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentNullException>(() => 
                service.UpdateTypeSpecFileAsync("test.tsp", null!, validationContext, CancellationToken.None));
            Assert.That(exception!.ParamName, Is.EqualTo("content"));
        }

        [Test]
        public void UpdateTypeSpecFileAsync_WithNullValidationContext_ShouldThrowArgumentNullException()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentNullException>(() => 
                service.UpdateTypeSpecFileAsync("test.tsp", "content", null!, CancellationToken.None));
            Assert.That(exception!.ParamName, Is.EqualTo("validationContext"));
        }

        [Test]
        public void UpdateTypeSpecFileAsync_WithPathTraversalAttempt_ShouldThrowSecurityException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act & Assert
            var exception = Assert.ThrowsAsync<SecurityException>(() => 
                service.UpdateTypeSpecFileAsync("../../../evil.tsp", "malicious content", validationContext, CancellationToken.None));
            Assert.That(exception!.Message, Does.Contain("attempts to write outside current directory"));
        }

        [Test]
        public void UpdateTypeSpecFileAsync_WithAbsolutePathOutsideDirectory_ShouldThrowSecurityException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act & Assert
            var exception = Assert.ThrowsAsync<SecurityException>(() => 
                service.UpdateTypeSpecFileAsync("C:\\temp\\evil.tsp", "malicious content", validationContext, CancellationToken.None));
            Assert.That(exception!.Message, Does.Contain("attempts to write outside current directory"));
        }


        [Test]
        public void GetTypeSpecFilesAsync_AfterDispose_ShouldThrowObjectDisposedException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act
            service.Dispose();

            // Assert
            Assert.ThrowsAsync<ObjectDisposedException>(() => 
                service.GetTypeSpecFilesAsync(validationContext, CancellationToken.None));
        }

        [Test]
        public void UpdateTypeSpecFileAsync_AfterDispose_ShouldThrowObjectDisposedException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act
            service.Dispose();

            // Assert
            Assert.ThrowsAsync<ObjectDisposedException>(() => 
                service.UpdateTypeSpecFileAsync("test.tsp", "content", validationContext, CancellationToken.None));
        }

        [Test]
        public void Dispose_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() => service.Dispose());
                Assert.DoesNotThrow(() => service.Dispose());
                Assert.DoesNotThrow(() => service.Dispose());
            });
        }

        [Test]
        public async Task UpdateTypeSpecFileAsync_WithEmptyContent_ShouldCreateEmptyFile()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act
            var result = await service.UpdateTypeSpecFileAsync("empty.tsp", "", validationContext, CancellationToken.None);

            // Assert
            var filePath = Path.Combine(typeSpecDir, "empty.tsp");
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(File.Exists(filePath), Is.True);
                Assert.That(File.ReadAllText(filePath), Is.EqualTo(""));
            });
        }

        [Test]
        public async Task UpdateTypeSpecFileAsync_WithVeryLongContent_ShouldHandleCorrectly()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            var longContent = new string('a', 1000000); // 1MB content

            // Act
            var result = await service.UpdateTypeSpecFileAsync("large.tsp", longContent, validationContext, CancellationToken.None);

            // Assert
            var filePath = Path.Combine(typeSpecDir, "large.tsp");
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(File.Exists(filePath), Is.True);
                Assert.That(File.ReadAllText(filePath).Length, Is.EqualTo(1000000));
            });
        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithReadOnlyFile_ShouldReadCorrectly()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            // Make main.tsp read-only
            var mainFile = Path.Combine(typeSpecDir, "main.tsp");
            File.SetAttributes(mainFile, FileAttributes.ReadOnly);

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            try
            {
                // Act
                var result = await service.GetTypeSpecFilesAsync(validationContext, CancellationToken.None);

                // Assert
                Assert.Multiple(() =>
                {
                    Assert.That(result, Is.Not.Null);
                    Assert.That(result.ContainsKey("main.tsp"), Is.True);
                    Assert.That(result["main.tsp"], Does.Contain("Valid TypeSpec content"));
                });
            }
            finally
            {
                // Cleanup: Remove read-only attribute
                File.SetAttributes(mainFile, FileAttributes.Normal);
            }
        }
    }
}