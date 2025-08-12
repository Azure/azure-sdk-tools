using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class TypeSpecFileServiceTests
    {
        #region Helper Methods

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
            ValidationContext? validationContext = null)
        {
            return new TypeSpecFileService(
                appSettings ?? CreateAppSettings(),
                (mockLogger ?? CreateMockLogger()).Object,
                (mockLoggerFactory ?? CreateMockLoggerFactory()).Object,
                validationContext ?? CreateLocalValidationContext("C:\\temp\\typespec", "C:\\temp\\output"));
        }

        #endregion

        #region Test Environment Fixture

        private sealed class TestEnvironmentFixture : IDisposable
        {
            private readonly string _tempDirectory;
            private bool _disposed;

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

        #endregion

        #region Constructor Tests

        [Test]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange
            var appSettings = CreateAppSettings();
            var mockLogger = CreateMockLogger();
            var mockLoggerFactory = CreateMockLoggerFactory();
            var validationContext = CreateLocalValidationContext("C:\\temp\\typespec", "C:\\temp\\output");

            // Act
            var service = new TypeSpecFileService(appSettings, mockLogger.Object, mockLoggerFactory.Object, validationContext);

            // Assert
            Assert.That(service, Is.Not.Null);
            service.Dispose();
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            // Arrange
            var mockLogger = CreateMockLogger();
            var mockLoggerFactory = CreateMockLoggerFactory();
            var validationContext = CreateLocalValidationContext("C:\\temp\\typespec", "C:\\temp\\output");

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new TypeSpecFileService(null!, mockLogger.Object, mockLoggerFactory.Object, validationContext));
            Assert.That(exception!.ParamName, Is.EqualTo("appSettings"));
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange
            var appSettings = CreateAppSettings();
            var mockLoggerFactory = CreateMockLoggerFactory();
            var validationContext = CreateLocalValidationContext("C:\\temp\\typespec", "C:\\temp\\output");

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new TypeSpecFileService(appSettings, null!, mockLoggerFactory.Object, validationContext));
            Assert.That(exception!.ParamName, Is.EqualTo("logger"));
        }

        [Test]
        public void Constructor_WithNullLoggerFactory_ShouldThrowArgumentNullException()
        {
            // Arrange
            var appSettings = CreateAppSettings();
            var mockLogger = CreateMockLogger();
            var validationContext = CreateLocalValidationContext("C:\\temp\\typespec", "C:\\temp\\output");

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new TypeSpecFileService(appSettings, mockLogger.Object, null!, validationContext));
            Assert.That(exception!.ParamName, Is.EqualTo("loggerFactory"));
        }

        [Test]
        public void Constructor_WithNullValidationContext_ShouldThrowArgumentNullException()
        {
            // Arrange
            var appSettings = CreateAppSettings();
            var mockLogger = CreateMockLogger();
            var mockLoggerFactory = CreateMockLoggerFactory();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new TypeSpecFileService(appSettings, mockLogger.Object, mockLoggerFactory.Object, null!));
            Assert.That(exception!.ParamName, Is.EqualTo("validationContext"));
        }

        #endregion

        #region Local TypeSpec File Tests

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
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value.Count, Is.GreaterThan(0));

            service.Dispose();
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
            Assert.ThrowsAsync<TaskCanceledException>(
                async () => await service.GetTypeSpecFilesAsync(cts.Token));
            service.Dispose();
        }

        #endregion

        #region GitHub TypeSpec File Tests

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

            var service = new TypeSpecFileService(appSettings, mockLogger.Object, mockLoggerFactory.Object, validationContext);

            // Act & Assert - Just verify the service was created (GitHub service creation will happen on demand)
            Assert.That(service, Is.Not.Null);

            service.Dispose();
        }

        [Test]
        public void Dispose_ShouldDisposeGitHubService()
        {
            // Arrange
            using var fixture = new TestEnvironmentFixture();
            var typeSpecPath = fixture.CreateValidGitHubTypeSpecPath();
            var commitId = fixture.CreateValidCommitId();
            var outputDir = fixture.CreateValidOutputDirectory();

            var validationContext = CreateGitHubValidationContext(typeSpecPath, commitId, outputDir);
            var service = CreateService(validationContext: validationContext);

            // Act & Assert - Just verify dispose doesn't throw
            Assert.DoesNotThrow(() => service.Dispose());
        }

        #endregion
    }
}
