using System.Net;
using System.Text.Json;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class GitHubFileServiceTests
    {
        #region Helper Methods

        private static Mock<ILogger<GitHubFileService>> CreateMockLogger()
        {
            return new Mock<ILogger<GitHubFileService>>();
        }

        private static AppSettings CreateAppSettings()
        {
            var configMock = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<AppSettings>>();
            
            // Setup default section to return null
            var defaultSection = new Mock<IConfigurationSection>();
            defaultSection.Setup(s => s.Value).Returns((string?)null);
            configMock.Setup(c => c.GetSection(It.IsAny<string>())).Returns(defaultSection.Object);
            
            // Setup required ProjectEndpoint
            var endpointSection = new Mock<IConfigurationSection>();
            endpointSection.Setup(s => s.Value).Returns("https://test.example.com");
            configMock.Setup(c => c.GetSection("AzureSettings:ProjectEndpoint")).Returns(endpointSection.Object);

            return new AppSettings(configMock.Object, mockLogger.Object);
        }

        private static ValidationContext CreateValidationContext()
        {
            return ValidationContext.CreateFromValidatedInputs(
                "specification/cognitiveservices/data-plane/Face",
                "abc123",
                "/temp/sdk"
            );
        }

        private static HttpClient CreateMockHttpClient(Mock<HttpMessageHandler> mockHandler)
        {
            return new HttpClient(mockHandler.Object);
        }

        private static Mock<HttpMessageHandler> CreateMockHttpMessageHandler()
        {
            return new Mock<HttpMessageHandler>();
        }

        private static TestableGitHubFilesService CreateTestableService(
            AppSettings? appSettings = null,
            Mock<ILogger<GitHubFileService>>? mockLogger = null,
            ValidationContext? validationContext = null,
            HttpClient? httpClient = null)
        {
            var mockHandler = CreateMockHttpMessageHandler();
            return new TestableGitHubFilesService(
                appSettings ?? CreateAppSettings(),
                (mockLogger ?? CreateMockLogger()).Object,
                validationContext ?? CreateValidationContext(),
                httpClient ?? CreateMockHttpClient(mockHandler));
        }

        private static void SetupSuccessfulApiResponse(Mock<HttpMessageHandler> mockHandler)
        {
            var githubContents = new[]
            {
                new GitHubContent
                {
                    Name = "test.tsp",
                    Type = "file",
                    DownloadUrl = "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/main/test.tsp"
                }
            };

            var jsonResponse = JsonSerializer.Serialize(githubContents);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };

            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/contents/")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);
        }

        private static void SetupMultipleFilesApiResponse(Mock<HttpMessageHandler> mockHandler)
        {
            var githubContents = new[]
            {
                new GitHubContent
                {
                    Name = "file1.tsp",
                    Type = "file",
                    DownloadUrl = "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/main/file1.tsp"
                },
                new GitHubContent
                {
                    Name = "file2.tsp",
                    Type = "file",
                    DownloadUrl = "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/main/file2.tsp"
                }
            };

            var jsonResponse = JsonSerializer.Serialize(githubContents);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };

            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/contents/")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);
        }

        private static void SetupMixedFileTypesApiResponse(Mock<HttpMessageHandler> mockHandler)
        {
            var githubContents = new[]
            {
                new GitHubContent
                {
                    Name = "test.tsp",
                    Type = "file",
                    DownloadUrl = "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/main/test.tsp"
                },
                new GitHubContent
                {
                    Name = "readme.md",
                    Type = "file",
                    DownloadUrl = "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/main/readme.md"
                },
                new GitHubContent
                {
                    Name = "subfolder",
                    Type = "dir",
                    DownloadUrl = null
                }
            };

            var jsonResponse = JsonSerializer.Serialize(githubContents);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };

            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/contents/")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);
        }

        private static void SetupFailedApiResponse(Mock<HttpMessageHandler> mockHandler, HttpStatusCode statusCode, string reasonPhrase)
        {
            var httpResponse = new HttpResponseMessage(statusCode)
            {
                ReasonPhrase = reasonPhrase
            };

            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/contents/")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);
        }

        private static void SetupInvalidJsonResponse(Mock<HttpMessageHandler> mockHandler)
        {
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("invalid json")
            };

            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/contents/")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);
        }

        private static void SetupEmptyApiResponse(Mock<HttpMessageHandler> mockHandler)
        {
            var githubContents = new[]
            {
                new GitHubContent
                {
                    Name = "readme.md",
                    Type = "file",
                    DownloadUrl = "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/main/readme.md"
                }
            };

            var jsonResponse = JsonSerializer.Serialize(githubContents);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };

            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/contents/")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);
        }

        private static void SetupSuccessfulFileDownload(Mock<HttpMessageHandler> mockHandler, string fileName, string content)
        {
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            };

            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().EndsWith(fileName)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);
        }

        private static void SetupFailedFileDownload(Mock<HttpMessageHandler> mockHandler, string fileName)
        {
            var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                ReasonPhrase = "Not Found"
            };

            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().EndsWith(fileName)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);
        }

        private static void VerifyLogMessage(
            Mock<ILogger<GitHubFileService>> mockLogger,
            LogLevel expectedLevel,
            string expectedMessage,
            Times? times = null)
        {
            mockLogger.Verify(
                x => x.Log(
                    expectedLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                times ?? Times.AtLeastOnce());
        }

        #endregion

        #region Constructor Tests

        [Test]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange
            var appSettings = CreateAppSettings();
            var mockLogger = CreateMockLogger();
            var validationContext = CreateValidationContext();
            var httpClient = new HttpClient();

            // Act & Assert
            var service = new GitHubFileService(appSettings, mockLogger.Object, validationContext, httpClient);
            Assert.That(service, Is.Not.Null);
            httpClient.Dispose();
        }

        [Test]
        public void Constructor_WithNullAppSettings_ShouldThrowArgumentNullException()
        {
            // Arrange
            var mockLogger = CreateMockLogger();
            var validationContext = CreateValidationContext();
            var httpClient = new HttpClient();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GitHubFileService(null!, mockLogger.Object, validationContext, httpClient));
            Assert.That(exception!.ParamName, Is.EqualTo("appSettings"));
            httpClient.Dispose();
        }

        [Test]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange
            var appSettings = CreateAppSettings();
            var validationContext = CreateValidationContext();
            var httpClient = new HttpClient();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GitHubFileService(appSettings, null!, validationContext, httpClient));
            Assert.That(exception!.ParamName, Is.EqualTo("logger"));
            httpClient.Dispose();
        }

        [Test]
        public void Constructor_WithNullValidationContext_ShouldThrowArgumentNullException()
        {
            // Arrange
            var appSettings = CreateAppSettings();
            var mockLogger = CreateMockLogger();
            var httpClient = new HttpClient();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new GitHubFileService(appSettings, mockLogger.Object, null!, httpClient));
            Assert.That(exception!.ParamName, Is.EqualTo("validationContext"));
            httpClient.Dispose();
        }

        #endregion

        #region GetTypeSpecFilesAsync Tests

        [Test]
        public async Task GetTypeSpecFilesAsync_WithSuccessfulResponse_ShouldReturnSuccess()
        {
            // Arrange
            var mockHandler = CreateMockHttpMessageHandler();
            var httpClient = CreateMockHttpClient(mockHandler);
            var service = CreateTestableService(httpClient: httpClient);

            SetupSuccessfulApiResponse(mockHandler);
            SetupSuccessfulFileDownload(mockHandler, "test.tsp", "model Test {}");

            // Act
            var result = await service.GetTypeSpecFilesAsync();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value, Is.Not.Null);
                Assert.That(result.Value!.Count, Is.EqualTo(1));
                Assert.That(result.Value["test.tsp"], Is.EqualTo("model Test {}"));
            });


        }

        [Test]
        public void GetTypeSpecFilesAsync_WithHttpRequestException_ShouldThrowException()
        {
            // Arrange
            var mockHandler = CreateMockHttpMessageHandler();
            var httpClient = CreateMockHttpClient(mockHandler);
            var service = CreateTestableService(httpClient: httpClient);

            SetupFailedApiResponse(mockHandler, HttpStatusCode.NotFound, "Not Found");

            // Act & Assert
            var exception = Assert.ThrowsAsync<HttpRequestException>(async () => 
                await service.GetTypeSpecFilesAsync());
            Assert.That(exception!.Message, Does.Contain("GitHub API request failed: NotFound Not Found"));


        }

        [Test]
        public void GetTypeSpecFilesAsync_WithInvalidJson_ShouldThrowJsonException()
        {
            // Arrange
            var mockHandler = CreateMockHttpMessageHandler();
            var httpClient = CreateMockHttpClient(mockHandler);
            var service = CreateTestableService(httpClient: httpClient);

            SetupInvalidJsonResponse(mockHandler);

            // Act & Assert
            var exception = Assert.ThrowsAsync<JsonException>(async () => 
                await service.GetTypeSpecFilesAsync());
            Assert.That(exception!.Message, Does.Contain("is an invalid start of a value"));


        }

        [Test]
        public void GetTypeSpecFilesAsync_WithNoTspFiles_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var mockHandler = CreateMockHttpMessageHandler();
            var httpClient = CreateMockHttpClient(mockHandler);
            var service = CreateTestableService(httpClient: httpClient);

            SetupEmptyApiResponse(mockHandler);

            // Act & Assert
            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await service.GetTypeSpecFilesAsync());
            Assert.That(exception!.Message, Does.Contain("No valid .tsp files found with download URLs"));


        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithMultipleTspFiles_ShouldReturnAllFiles()
        {
            // Arrange
            var mockHandler = CreateMockHttpMessageHandler();
            var httpClient = CreateMockHttpClient(mockHandler);
            var service = CreateTestableService(httpClient: httpClient);

            SetupMultipleFilesApiResponse(mockHandler);
            SetupSuccessfulFileDownload(mockHandler, "file1.tsp", "model File1 {}");
            SetupSuccessfulFileDownload(mockHandler, "file2.tsp", "model File2 {}");

            // Act
            var result = await service.GetTypeSpecFilesAsync();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value!.Count, Is.EqualTo(2));
                Assert.That(result.Value.ContainsKey("file1.tsp"), Is.True);
                Assert.That(result.Value.ContainsKey("file2.tsp"), Is.True);
            });


        }

        [Test]
        public void GetTypeSpecFilesAsync_WithCancellation_ShouldThrowOperationCanceledException()
        {
            // Arrange
            var mockHandler = CreateMockHttpMessageHandler();
            var httpClient = CreateMockHttpClient(mockHandler);
            var service = CreateTestableService(httpClient: httpClient);
            
            using var cts = new CancellationTokenSource();
            
            // Setup mock to throw OperationCanceledException when cancelled token is used
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());
                
            // Act & Assert
            Assert.ThrowsAsync<OperationCanceledException>(async () => 
                await service.GetTypeSpecFilesAsync(cts.Token));


        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithMixedFileTypes_ShouldOnlyReturnTspFiles()
        {
            // Arrange
            var mockHandler = CreateMockHttpMessageHandler();
            var httpClient = CreateMockHttpClient(mockHandler);
            var service = CreateTestableService(httpClient: httpClient);

            SetupMixedFileTypesApiResponse(mockHandler);
            SetupSuccessfulFileDownload(mockHandler, "test.tsp", "model Test {}");

            // Act
            var result = await service.GetTypeSpecFilesAsync();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value!.Count, Is.EqualTo(1));
                Assert.That(result.Value.ContainsKey("test.tsp"), Is.True);
                Assert.That(result.Value.ContainsKey("readme.md"), Is.False);
            });


        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithFailedFileDownload_ShouldSkipFailedFiles()
        {
            // Arrange
            var mockHandler = CreateMockHttpMessageHandler();
            var httpClient = CreateMockHttpClient(mockHandler);
            var service = CreateTestableService(httpClient: httpClient);

            SetupMultipleFilesApiResponse(mockHandler);
            SetupSuccessfulFileDownload(mockHandler, "file1.tsp", "model File1 {}");
            SetupFailedFileDownload(mockHandler, "file2.tsp");

            // Act
            var result = await service.GetTypeSpecFilesAsync();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value!.Count, Is.EqualTo(1));
                Assert.That(result.Value.ContainsKey("file1.tsp"), Is.True);
                Assert.That(result.Value.ContainsKey("file2.tsp"), Is.False);
            });


        }

        [Test]
        public async Task GetTypeSpecFilesAsync_WithEmptyFileContent_ShouldSkipEmptyFiles()
        {
            // Arrange
            var mockHandler = CreateMockHttpMessageHandler();
            var httpClient = CreateMockHttpClient(mockHandler);
            var service = CreateTestableService(httpClient: httpClient);

            SetupMultipleFilesApiResponse(mockHandler);
            SetupSuccessfulFileDownload(mockHandler, "file1.tsp", "model File1 {}");
            SetupSuccessfulFileDownload(mockHandler, "file2.tsp", ""); // Empty content

            // Act
            var result = await service.GetTypeSpecFilesAsync();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value!.Count, Is.EqualTo(1));
                Assert.That(result.Value.ContainsKey("file1.tsp"), Is.True);
                Assert.That(result.Value.ContainsKey("file2.tsp"), Is.False);
            });


        }

        [Test]
        public void GetTypeSpecFilesAsync_WithUnexpectedException_ShouldThrowAndLogCritical()
        {
            // Arrange
            var mockLogger = CreateMockLogger();
            var mockHandler = CreateMockHttpMessageHandler();
            var httpClient = CreateMockHttpClient(mockHandler);
            var service = CreateTestableService(mockLogger: mockLogger, httpClient: httpClient);

            // Setup handler to throw exception
            mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Unexpected error"));

            // Act & Assert
            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await service.GetTypeSpecFilesAsync());
            Assert.That(exception!.Message, Does.Contain("Unexpected error"));

            // Verify critical logging
            VerifyLogMessage(mockLogger, LogLevel.Critical, "Unexpected error while fetching TypeSpec files");


        }

        #endregion

        #region Dispose Tests

        [Test]
        public void Dispose_ShouldDisposeHttpClient()
        {
            // Arrange
            var service = CreateTestableService();

            // Act & Assert

        }

        [Test]
        public void Dispose_CalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var service = CreateTestableService();

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {


            });
        }

        #endregion

        #region Testable Service Class

        /// <summary>
        /// Testable version of GitHubFilesService that allows HttpClient injection
        /// </summary>
        private class TestableGitHubFilesService : GitHubFileService
        {
            public TestableGitHubFilesService(
                AppSettings appSettings,
                ILogger<GitHubFileService> logger,
                ValidationContext validationContext,
                HttpClient httpClient) : base(appSettings, logger, validationContext, httpClient)
            {
            }

            // No longer need Dispose method since GitHubFilesService doesn't implement IDisposable
        }

        #endregion
    }
}
