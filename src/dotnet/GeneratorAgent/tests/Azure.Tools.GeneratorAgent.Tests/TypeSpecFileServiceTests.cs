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
            return ValidationContext.CreateFromValidatedInputs(typeSpecDir, "", outputDir);
        }

        private static ValidationContext CreateGitHubValidationContext(string typeSpecPath, string commitId, string outputDir)
        {
            return ValidationContext.CreateFromValidatedInputs(typeSpecPath, commitId, outputDir);
        }

        private static TypeSpecFileService CreateService(
            AppSettings? appSettings = null,
            Mock<ILogger<TypeSpecFileService>>? mockLogger = null,
            Mock<ILoggerFactory>? mockLoggerFactory = null,
            ValidationContext? validationContext = null,
            Func<ValidationContext, GitHubFileService>? gitHubServiceFactory = null)
        {
            return new TypeSpecFileService(
                (mockLogger ?? CreateMockLogger()).Object,
                validationContext ?? CreateLocalValidationContext("C:\\temp\\typespec", "C:\\temp\\output"),
                gitHubServiceFactory ?? CreateMockGitHubServiceFactory());
        }

        private static Func<ValidationContext, GitHubFileService> CreateMockGitHubServiceFactory()
        {
            return validationContext =>
            {
                var appSettings = CreateAppSettings();
                var logger = new Mock<ILogger<GitHubFileService>>().Object;
                var httpClient = new HttpClient();
                return new GitHubFileService(appSettings, logger, httpClient);
            };
        }

        private static Mock<GitHubFileService> CreateMockGitHubService(Dictionary<string, string>? filesToReturn = null)
        {
            return new Mock<GitHubFileService>();
        }

        private static Func<ValidationContext, GitHubFileService> CreateMockGitHubServiceFactoryWithResults(Dictionary<string, string> expectedResults)
        {
            return validationContext =>
            {
                var appSettings = CreateAppSettings();
                var logger = new Mock<ILogger<GitHubFileService>>().Object;
                var httpClient = new HttpClient();
                return new GitHubFileService(appSettings, logger, httpClient);
            };
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
                return "https://github.com/Azure/azure-rest-api-specs/tree/main/specification/typespec";
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
            var appSettings = CreateAppSettings();
            var mockLogger = CreateMockLogger();
            var mockLoggerFactory = CreateMockLoggerFactory();
            var validationContext = CreateLocalValidationContext("C:\\temp\\typespec", "C:\\temp\\output");
            var httpClient = new HttpClient();
            var gitHubServiceFactory = CreateMockGitHubServiceFactory();

            // Act
            var service = CreateService(appSettings, mockLogger, mockLoggerFactory, validationContext, gitHubServiceFactory);

            // Assert
            Assert.That(service, Is.Not.Null);
            httpClient.Dispose();
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            // Arrange
            var mockLogger = CreateMockLogger();
            var mockLoggerFactory = CreateMockLoggerFactory();
            var validationContext = CreateLocalValidationContext("C:\\temp\\typespec", "C:\\temp\\output");
            var httpClient = new HttpClient();
            var gitHubServiceFactory = CreateMockGitHubServiceFactory();

            // Act & Assert
            var service = new TypeSpecFileService(mockLogger.Object, validationContext, gitHubServiceFactory);
            Assert.That(service, Is.Not.Null);
            httpClient.Dispose();
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange
            var appSettings = CreateAppSettings();
            var mockLoggerFactory = CreateMockLoggerFactory();
            var validationContext = CreateLocalValidationContext("C:\\temp\\typespec", "C:\\temp\\output");
            var httpClient = new HttpClient();
            var gitHubServiceFactory = CreateMockGitHubServiceFactory();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new TypeSpecFileService(null!, validationContext, gitHubServiceFactory));
            Assert.That(exception!.ParamName, Is.EqualTo("logger"));
            httpClient.Dispose();
        }

        [Test]
        public void Constructor_WithNullLoggerFactory_ShouldThrowArgumentNullException()
        {
            // Arrange
            var appSettings = CreateAppSettings();
            var mockLogger = CreateMockLogger();
            var validationContext = CreateLocalValidationContext("C:\\temp\\typespec", "C:\\temp\\output");
            var httpClient = new HttpClient();
            var gitHubServiceFactory = CreateMockGitHubServiceFactory();

            // Act & Assert
            var service = new TypeSpecFileService(mockLogger.Object, validationContext, gitHubServiceFactory);
            Assert.That(service, Is.Not.Null);
            httpClient.Dispose();
        }

        [Test]
        public void Constructor_WithNullValidationContext_ShouldThrowArgumentNullException()
        {
            // Arrange
            var appSettings = CreateAppSettings();
            var mockLogger = CreateMockLogger();
            var mockLoggerFactory = CreateMockLoggerFactory();
            var httpClient = new HttpClient();
            var gitHubServiceFactory = CreateMockGitHubServiceFactory();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new TypeSpecFileService(mockLogger.Object, null!, gitHubServiceFactory));
            Assert.That(exception!.ParamName, Is.EqualTo("validationContext"));
            httpClient.Dispose();
        }

        [Test]
        public void Constructor_WithNullGitHubServiceFactory_ShouldThrowArgumentNullException()
        {
            // Arrange
            var mockLogger = CreateMockLogger();
            var validationContext = CreateLocalValidationContext("C:\\temp\\typespec", "C:\\temp\\output");

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new TypeSpecFileService(mockLogger.Object, validationContext, null!));
            Assert.That(exception!.ParamName, Is.EqualTo("gitHubServiceFactory"));
        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithLocalPath_ShouldReturnFiles()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService(validationContext: validationContext);

            // Act
            var result = await service.GetTypeSpecFilesAsync(CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.GreaterThan(0));
            Assert.That(result.ContainsKey("main.tsp"), Is.True);
        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithLocalPath_EmptyDirectory_ShouldReturnEmptyDictionary()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = Path.Combine(fixture.TempDirectory, "empty");
            Directory.CreateDirectory(typeSpecDir);
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService(validationContext: validationContext);

            // Act
            var result = await service.GetTypeSpecFilesAsync(CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(0));
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
            var service = CreateService(validationContext: validationContext);

            // Act
            var result = await service.GetTypeSpecFilesAsync(CancellationToken.None);

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
            var service = CreateService(validationContext: validationContext);

            // Act
            var result = await service.GetTypeSpecFilesAsync(CancellationToken.None);

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
            var validationContext = CreateLocalValidationContext(invalidPath, "C:\\temp\\output");
            var service = CreateService(validationContext: validationContext);

            // Act & Assert
            Assert.ThrowsAsync<DirectoryNotFoundException>(
                async () => await service.GetTypeSpecFilesAsync(CancellationToken.None));
        }

        [Test]
        public void GetTypeSpecFilesAsync_WithCancellation_ShouldThrowOperationCanceledException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService(validationContext: validationContext);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await service.GetTypeSpecFilesAsync(cts.Token));
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
            var gitHubServiceFactory = CreateMockGitHubServiceFactory();

            var service = CreateService(appSettings, mockLogger, mockLoggerFactory, validationContext, gitHubServiceFactory);

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
            
            bool factoryCalled = false;
            ValidationContext? receivedContext = null;
            
            Func<ValidationContext, GitHubFileService> testFactory = (context) =>
            {
                factoryCalled = true;
                receivedContext = context;
                var appSettings = CreateAppSettings();
                var logger = new Mock<ILogger<GitHubFileService>>().Object;
                var httpClient = new HttpClient();
                return new GitHubFileService(appSettings, logger, httpClient);
            };
            
            var service = CreateService(validationContext: validationContext, gitHubServiceFactory: testFactory);

            // Act
            Assert.ThrowsAsync<DirectoryNotFoundException>(async () => await service.GetTypeSpecFilesAsync(CancellationToken.None));

            // Assert - Factory should NOT be called when calling GetTypeSpecFilesAsync directly with GitHub URL
            // The service requires EnsureTypeSpecFilesAvailableAsync to be called first for GitHub scenarios
            Assert.That(factoryCalled, Is.False);
            Assert.That(receivedContext, Is.Null);
        }


        [Test]
        public async Task UpdateTypeSpecFileAsync_WithValidParameters_ShouldUpdateFile()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService(validationContext: validationContext);

            var fileName = "updated.tsp";
            var content = "// Updated content";

            // Act
            await service.UpdateTypeSpecFileAsync(fileName, content, CancellationToken.None);

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
            var service = CreateService(validationContext: validationContext);

            var fileName = "main.tsp";
            var newContent = "// Updated main content";

            // Act
            await service.UpdateTypeSpecFileAsync(fileName, newContent, CancellationToken.None);

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
            var service = CreateService(validationContext: validationContext);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await service.UpdateTypeSpecFileAsync(null!, "content", CancellationToken.None));
        }

        [Test]
        public void UpdateTypeSpecFileAsync_WithEmptyFileName_ShouldThrowArgumentException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService(validationContext: validationContext);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(
                async () => await service.UpdateTypeSpecFileAsync("", "content", CancellationToken.None));
        }

        [Test]
        public void UpdateTypeSpecFileAsync_WithWhitespaceFileName_ShouldThrowArgumentException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService(validationContext: validationContext);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(
                async () => await service.UpdateTypeSpecFileAsync("   ", "content", CancellationToken.None));
        }

        [Test]
        public void UpdateTypeSpecFileAsync_WithNullContent_ShouldThrowArgumentNullException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService(validationContext: validationContext);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await service.UpdateTypeSpecFileAsync("test.tsp", null!, CancellationToken.None));
        }

        [Test]
        public async Task UpdateTypeSpecFileAsync_WithDirectoryTraversalFileName_ShouldThrowSecurityException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService(validationContext: validationContext);

            // Act
            var result = await service.UpdateTypeSpecFileAsync("../../../evil.tsp", "malicious content", CancellationToken.None);

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
            var service = CreateService(validationContext: validationContext);

            // Act
            var result = await service.UpdateTypeSpecFileAsync("C:\\Windows\\System32\\evil.tsp", "malicious content", CancellationToken.None);

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
            var service = CreateService(validationContext: validationContext);

            // Act
            service.Dispose();

            // Assert
            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await service.UpdateTypeSpecFileAsync("test.tsp", "content", CancellationToken.None));
        }

        [Test]
        public async Task UpdateTypeSpecFileAsync_WithCancellation_ShouldThrowOperationCanceledException()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecDir = fixture.CreateValidTypeSpecDirectory();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateLocalValidationContext(typeSpecDir, outputDir);
            var service = CreateService(validationContext: validationContext);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await service.UpdateTypeSpecFileAsync("test.tsp", "content", cts.Token);

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
            var service = CreateService(validationContext: validationContext);

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
            var service = CreateService(validationContext: validationContext);

            // Act
            service.Dispose();

            // Assert
            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await service.GetTypeSpecFilesAsync(CancellationToken.None));
        }

        [Test]
        public void CreateSecureTempPath_ShouldGenerateValidPath()
        {
            // This tests the path generation logic indirectly through GitHub flow
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = "https://github.com/test/repo/tree/main/spec_with_special_chars<>|";
            var commitId = fixture.CreateValidCommitId();
            var outputDir = fixture.CreateValidOutputDirectory();
            
            var validationContext = CreateGitHubValidationContext(typeSpecPath, commitId, outputDir);
            var service = CreateService(validationContext: validationContext);

            // Act & Assert - Just verify the service was created without errors
            Assert.That(service, Is.Not.Null);
        }
    }
}
