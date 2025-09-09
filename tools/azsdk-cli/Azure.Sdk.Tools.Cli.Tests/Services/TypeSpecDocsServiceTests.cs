using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    [TestFixture]
    public class TypeSpecDocsServiceTests
    {
        private TypeSpecDocsService _service;
        private Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private Mock<IHttpClientFactory> _httpClientFactoryMock;

        [SetUp]
        public void SetUp()
        {
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();

            // Setup the mock to return a new HttpClient instance each time to avoid disposal issues
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(_httpMessageHandlerMock.Object));

            // Use a single test source to make tests predictable
            var testSources = new[] { "https://test.example.com/llms.json" };
            _service = new TypeSpecDocsService(NullLogger<TypeSpecDocsService>.Instance, _httpClientFactoryMock.Object, testSources);
        }

        [TearDown]
        public void TearDown() { }

        [Test]
        public async Task GetTopicsAsync_WithValidResponse_ReturnsSuccessfulResponse()
        {
            // Arrange
            var expectedTopics = new List<LlmsJsonItem>
            {
                new() { Topic = "getting-started", Description = "Getting started with TypeSpec", ContentUrl = "https://example.com/getting-started.md" },
                new() { Topic = "data-types", Description = "TypeSpec data types", ContentUrl = "https://example.com/data-types.md" }
            };

            var jsonResponse = JsonSerializer.Serialize(expectedTopics);
            SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

            // Act
            var result = await _service.GetTopicsAsync();

            Assert.Multiple(() =>
            {
                // Assert
                Assert.That(result.IsSuccessful, Is.True);
                Assert.That(result.Topics, Has.Count.EqualTo(2));
            });

            Assert.Multiple(() =>
            {
                Assert.That(result.Topics[0].Topic, Is.EqualTo("data-types"));
                Assert.That(result.Topics[0].Description, Is.EqualTo("TypeSpec data types"));
                Assert.That(result.Topics[1].Topic, Is.EqualTo("getting-started"));
                Assert.That(result.Topics[1].Description, Is.EqualTo("Getting started with TypeSpec"));
            });

        }

        [Test]
        public async Task GetTopicsAsync_WithHttpError_ReturnsFailureResponse()
        {
            SetupHttpResponse(HttpStatusCode.NotFound, "Not Found");

            var result = await _service.GetTopicsAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.False);
                // When all sources fail to load topics, it returns "No topics could be loaded from any source"
                Assert.That(result.ResponseError, Is.EqualTo("No topics could be loaded from any source"));
            });

        }

        [Test]
        public async Task GetTopicsAsync_WithInvalidJson_ReturnsFailureResponse()
        {
            // Arrange - Both default sources will return invalid JSON
            SetupHttpResponse(HttpStatusCode.OK, "invalid json");

            // Act
            var result = await _service.GetTopicsAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.False);
                // When all sources fail to parse, no topics are loaded
                Assert.That(result.ResponseError, Is.EqualTo("No topics could be loaded from any source"));
            });

        }

        [Test]
        public async Task GetTopicsAsync_WithEmptyResponse_ReturnsFailureResponse()
        {
            SetupHttpResponse(HttpStatusCode.OK, "[]");

            // Act
            var result = await _service.GetTopicsAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.ResponseError, Is.EqualTo("No topics could be loaded from any source"));
            });

        }

        [Test]
        public async Task GetTopicsAsync_CalledMultipleTimes_UsesCachedResult()
        {
            // Arrange
            var topics = new List<LlmsJsonItem>
            {
                new() { Topic = "test-topic", Description = "Test description", ContentUrl = "https://example.com/test.md" }
            };

            var jsonResponse = JsonSerializer.Serialize(topics);
            SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

            // Act
            var result1 = await _service.GetTopicsAsync();
            var result2 = await _service.GetTopicsAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result1.IsSuccessful, Is.True);
                Assert.That(result2.IsSuccessful, Is.True);
                Assert.That(result1.Topics, Has.Count.EqualTo(1));
                Assert.That(result2.Topics, Has.Count.EqualTo(1));
            });


            // Verify HTTP was only called once on first invocation (single source), then cached for second call
            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Exactly(1), // Should only be called once due to caching
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Test]
        public async Task GetTopicDocsAsync_WithValidTopics_ReturnsSuccessfulResponse()
        {
            // Arrange
            var topics = new List<LlmsJsonItem>
            {
                new() { Topic = "getting-started", Description = "Getting started", ContentUrl = "https://example.com/getting-started.md" },
                new() { Topic = "data-types", Description = "Data types", ContentUrl = "https://example.com/data-types.md" }
            };

            var topicsJsonResponse = JsonSerializer.Serialize(topics);
            var docContent1 = "# Getting Started\nThis is the getting started guide.";
            var docContent2 = "# Data Types\nThis covers TypeSpec data types.";

            // Setup responses in order: first call for loading topics from single source,
            // then two calls for fetching content
            _httpMessageHandlerMock.Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(topicsJsonResponse) }) // Topics
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(docContent1) }) // Content 1
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(docContent2) }); // Content 2

            // Act
            var result = await _service.GetTopicDocsAsync(new List<string> { "getting-started", "data-types" });

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.True);
                Assert.That(result.Docs, Has.Count.EqualTo(2));
            });


            var gettingStartedDoc = result.Docs.First(d => d.Topic == "getting-started");
            var dataTypesDoc = result.Docs.First(d => d.Topic == "data-types");

            Assert.Multiple(() =>
            {
                Assert.That(gettingStartedDoc.Contents, Is.EqualTo(docContent1));
                Assert.That(dataTypesDoc.Contents, Is.EqualTo(docContent2));
            });

        }

        [Test]
        public async Task GetTopicDocsAsync_WithUnknownTopic_ReturnsFailureResponse()
        {
            // Arrange
            var topics = new List<LlmsJsonItem>
            {
                new() { Topic = "known-topic", Description = "Known topic", ContentUrl = "https://example.com/known.md" }
            };

            var jsonResponse = JsonSerializer.Serialize(topics);
            SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

            // Act
            var result = await _service.GetTopicDocsAsync(new List<string> { "unknown-topic" });

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.ResponseError, Does.Contain("Unknown topics: unknown-topic"));
            });

        }

        [Test]
        public async Task GetTopicDocsAsync_WithMixedKnownAndUnknownTopics_ReturnsFailureResponse()
        {
            // Arrange
            var topics = new List<LlmsJsonItem>
            {
                new() { Topic = "known-topic", Description = "Known topic", ContentUrl = "https://example.com/known.md" }
            };

            var jsonResponse = JsonSerializer.Serialize(topics);
            SetupHttpResponse(HttpStatusCode.OK, jsonResponse);

            // Act
            var result = await _service.GetTopicDocsAsync(new List<string> { "known-topic", "unknown-topic1", "unknown-topic2" });

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.ResponseError, Does.Contain("Unknown topics: unknown-topic1, unknown-topic2"));
            });

        }

        [Test]
        public async Task GetTopicDocsAsync_WithContentFetchError_ReturnsFailureResponse()
        {
            // Arrange
            var topics = new List<LlmsJsonItem>
            {
                new() { Topic = "test-topic", Description = "Test topic", ContentUrl = "https://example.com/test.md" }
            };

            var topicsJsonResponse = JsonSerializer.Serialize(topics);

            // Setup successful topics response but failed content fetch
            _httpMessageHandlerMock.Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(topicsJsonResponse) }) // Topics load successfully
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound)); // Content fetch fails

            // Act
            var result = await _service.GetTopicDocsAsync(new List<string> { "test-topic" });

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.ResponseError, Does.Contain("Failed to fetch documentation"));
            });

        }

        [Test]
        public async Task GetTopicDocsAsync_WithoutLoadingTopicsFirst_LoadsTopicsAutomatically()
        {
            // Arrange
            var topics = new List<LlmsJsonItem>
            {
                new() { Topic = "auto-loaded", Description = "Auto loaded topic", ContentUrl = "https://example.com/auto.md" }
            };

            var topicsJsonResponse = JsonSerializer.Serialize(topics);
            var docContent = "# Auto Loaded\nThis was automatically loaded.";

            _httpMessageHandlerMock.Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(topicsJsonResponse) }) // Topics
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(docContent) }); // Content

            // Act - Call GetTopicDocsAsync without calling GetTopicsAsync first
            var result = await _service.GetTopicDocsAsync(new List<string> { "auto-loaded" });

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccessful, Is.True);
                Assert.That(result.Docs, Has.Count.EqualTo(1));
            });

            Assert.Multiple(() =>
            {
                Assert.That(result.Docs[0].Topic, Is.EqualTo("auto-loaded"));
                Assert.That(result.Docs[0].Contents, Is.EqualTo(docContent));
            });

        }

        [Test]
        public async Task GetTopicDocsAsync_WithCachedContent_UsesCachedResult()
        {
            // Arrange
            var topics = new List<LlmsJsonItem>
            {
                new() { Topic = "cached-topic", Description = "Cached topic", ContentUrl = "https://example.com/cached.md" }
            };

            var topicsJsonResponse = JsonSerializer.Serialize(topics);
            var docContent = "# Cached Content\nThis should be cached.";

            _httpMessageHandlerMock.Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(topicsJsonResponse) }) // Topics
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(docContent) }); // Content (only called once)

            // Act - Call twice with the same topic
            var result1 = await _service.GetTopicDocsAsync(new List<string> { "cached-topic" });
            var result2 = await _service.GetTopicDocsAsync(new List<string> { "cached-topic" });

            Assert.Multiple(() =>
            {
                Assert.That(result1.IsSuccessful, Is.True);
                Assert.That(result2.IsSuccessful, Is.True);
                Assert.That(result1.Docs[0].Contents, Is.EqualTo(docContent));
                Assert.That(result2.Docs[0].Contents, Is.EqualTo(docContent));
            });


            // Verify content was only fetched once (2 calls total: 1 for topics loading, 1 for content)
            _httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Exactly(2),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        private void SetupHttpResponse(HttpStatusCode statusCode, string content)
        {
            _httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                });
        }
    }
}
