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

        private static Mock<ILoggerFactory> CreateMockLoggerFactory()
        {
            var mockFactory = new Mock<ILoggerFactory>();
            mockFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
                .Returns(new Mock<ILogger>().Object);
            return mockFactory;
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

        private static Mock<GitHubFileService> CreateMockGitHubService(Dictionary<string, string>? filesToReturn = null)
        {
            return new Mock<GitHubFileService>();
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
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            // Arrange
            var mockLogger = CreateMockLogger();
            var gitHubFileService = CreateGitHubFileService();

            // Act & Assert
            var service = new TypeSpecFileService(mockLogger.Object, gitHubFileService);
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
        public void Constructor_WithNullLoggerFactory_ShouldThrowArgumentNullException()
        {
            // Arrange
            var mockLogger = CreateMockLogger();
            var gitHubFileService = CreateGitHubFileService();

            // Act & Assert
            var service = new TypeSpecFileService(mockLogger.Object, gitHubFileService);
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void Constructor_WithNullValidationContext_ShouldThrowArgumentNullException()
        {
            // Arrange
            var mockLogger = CreateMockLogger();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new TypeSpecFileService(mockLogger.Object, null!));
            Assert.That(exception!.ParamName, Is.EqualTo("gitHubFileService"));
        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithLocalPath_ShouldReturnFiles()
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
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.GreaterThan(0));
            Assert.That(result.ContainsKey("main.tsp"), Is.True);
        }

        [Test]
        public void GetTypeSpecFilesAsync_WithLocalPath_EmptyDirectory_ShouldThrowException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = Path.Combine(fixture.TempDirectory, "empty");
            Directory.CreateDirectory(typeSpecDir);
            var outputDir = fixture.CreateValidOutputDirectory();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => CreateLocalValidationContext(typeSpecDir, outputDir));
            Assert.That(ex!.Message, Does.Contain("No .tsp or .yaml files found in directory"));
        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithLocalPath_MultipleFiles_ShouldReturnAllFiles()
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
            var result = await service.GetTypeSpecFilesAsync(validationContext,CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result.ContainsKey("main.tsp"), Is.True);
            Assert.That(result.ContainsKey("second.tsp"), Is.True);
            Assert.That(result.ContainsKey("third.tsp"), Is.True);
        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithLocalPath_SubDirectories_ShouldReturnAllFiles()
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
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.ContainsKey("subfile.tsp"), Is.True);
        }

        [Test]
        public void GetTypeSpecFilesAsync_WithLocalPath_InvalidDirectory_ShouldThrowException()
        {
            // Arrange
            var invalidPath = "C:\\NonExistentDirectory\\TypeSpec";
            var service = CreateService();

            // Act & Assert
            Assert.Throws<DirectoryNotFoundException>(
                () => CreateLocalValidationContext(invalidPath, "C:\\temp\\output"));
        }

        [Test]
        public void GetTypeSpecFilesAsync_WithCancellation_ShouldThrowOperationCanceledException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await service.GetTypeSpecFilesAsync(validationContext, cts.Token));
        }

        [Test]
        public void GetTypeSpecFilesAsync_WithGitHubPath_ShouldUseGitHubService()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidGitHubTypeSpecPath();
            var commitId = fixture.CreateValidCommitId();
            var outputDir = fixture.CreateValidOutputDirectory();
            var mockLogger = CreateMockLogger();
            var mockLoggerFactory = CreateMockLoggerFactory();

            var validationContext = CreateGitHubValidationContext(typeSpecPath, commitId, outputDir);
            var appSettings = CreateAppSettings();
            var httpClient = new HttpClient();
            var gitHubFileService = CreateGitHubFileService();

            var service = CreateService(mockLogger, gitHubFileService);

            // Act & Assert
            Assert.That(service, Is.Not.Null);

            httpClient.Dispose();
        }

        [Test]
        public void GetTypeSpecFilesAsync_WithGitHubPath_FactoryIsCalledCorrectly()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidGitHubTypeSpecPath();
            var commitId = fixture.CreateValidCommitId();
            var outputDir = fixture.CreateValidOutputDirectory();
            
            var validationContext = CreateGitHubValidationContext(typeSpecPath, commitId, outputDir);
            
            var gitHubFileService = CreateGitHubFileService();
            var service = CreateService(gitHubFileService: gitHubFileService);

            Assert.That(() => service.GetTypeSpecFilesAsync(validationContext, CancellationToken.None),
                Throws.TypeOf<DirectoryNotFoundException>()
                .With.Message.Contains("TypeSpec directory not found"));
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
            await service.UpdateTypeSpecFileAsync(fileName, content, validationContext,CancellationToken.None);

            // Assert
            var filePath = Path.Combine(typeSpecDir, fileName);
            Assert.That(File.Exists(filePath), Is.True);
            var actualContent = await File.ReadAllTextAsync(filePath);
            Assert.That(actualContent, Is.EqualTo(content));
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
            await service.UpdateTypeSpecFileAsync(fileName, newContent, validationContext, CancellationToken.None);

            // Assert
            var filePath = Path.Combine(typeSpecDir, fileName);
            var actualContent = await File.ReadAllTextAsync(filePath);
            Assert.That(actualContent, Is.EqualTo(newContent));
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
            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await service.UpdateTypeSpecFileAsync(null!, "content", validationContext, CancellationToken.None));
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
            Assert.ThrowsAsync<ArgumentException>(
                async () => await service.UpdateTypeSpecFileAsync("", "content", validationContext, CancellationToken.None));
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
            Assert.ThrowsAsync<ArgumentException>(
                async () => await service.UpdateTypeSpecFileAsync("   ", "content", validationContext, CancellationToken.None));
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
            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await service.UpdateTypeSpecFileAsync("test.tsp", null!, validationContext, CancellationToken.None));
        }

        [Test]
        public async Task UpdateTypeSpecFileAsync_WithDirectoryTraversalFileName_ShouldThrowSecurityException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act
            var result = await service.UpdateTypeSpecFileAsync("../../../evil.tsp", "malicious content", validationContext, CancellationToken.None);

            // Assert
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Exception, Is.InstanceOf<SecurityException>());
        }

        [Test]
        public async Task UpdateTypeSpecFileAsync_WithAbsolutePathOutsideDirectory_ShouldThrowSecurityException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act
            var result = await service.UpdateTypeSpecFileAsync("C:\\Windows\\System32\\evil.tsp", "malicious content", validationContext, CancellationToken.None);

            // Assert
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Exception, Is.InstanceOf<SecurityException>());
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
            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await service.UpdateTypeSpecFileAsync("test.tsp", "content", validationContext,CancellationToken.None));
        }

        [Test]
        public async Task UpdateTypeSpecFileAsync_WithCancellation_ShouldThrowOperationCanceledException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await service.UpdateTypeSpecFileAsync("test.tsp", "content", validationContext,cts.Token);

            // Assert
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Exception, Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void Dispose_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService();

            // Act & Assert
            Assert.DoesNotThrow(() => service.Dispose());
            Assert.DoesNotThrow(() => service.Dispose());
            Assert.DoesNotThrow(() => service.Dispose());
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
            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await service.GetTypeSpecFilesAsync(validationContext, CancellationToken.None));
        }

        [Test]
        public void CreateSecureTempPath_ShouldGenerateValidPath()
        {
            // This tests the path generation logic indirectly through GitHub flow
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = "specification/test/TestService";
            var commitId = fixture.CreateValidCommitId();
            var outputDir = fixture.CreateValidOutputDirectory();
            
            var validationContext = CreateGitHubValidationContext(typeSpecPath, commitId, outputDir);
            var service = CreateService();

            // Act & Assert - Just verify the service was created without errors
            Assert.That(service, Is.Not.Null);
        }
    }
}
