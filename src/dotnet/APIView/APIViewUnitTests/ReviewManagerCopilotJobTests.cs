using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using APIViewWeb;
using APIViewWeb.Hubs;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using APIViewWeb.Services;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace APIViewUnitTests;

public class ReviewManagerCopilotJobTests
{
    private readonly MockContainer _mocks;
    private readonly ReviewManager _reviewManager;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;

    public ReviewManagerCopilotJobTests()
    {
        _mocks = new MockContainer();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        _mocks.HttpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        _mocks.Configuration.Setup(c => c["CopilotServiceEndpoint"])
            .Returns("https://copilot.test");

        _mocks.CopilotAuth.Setup(c => c.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-token");

        _reviewManager = new ReviewManager(
            _mocks.AuthorizationService.Object,
            _mocks.ReviewsRepository.Object,
            _mocks.ApiRevisionsManager.Object,
            _mocks.CommentManager.Object,
            _mocks.CodeFileRepository.Object,
            _mocks.CommentsRepository.Object,
            _mocks.ApiRevisionsRepository.Object,
            _mocks.SignalRHubContext.Object,
            _mocks.LanguageServices,
            _mocks.TelemetryClient,
            _mocks.CodeFileManager.Object,
            _mocks.Configuration.Object,
            _mocks.HttpClientFactory.Object,
            _mocks.PollingJobQueueManager.Object,
            _mocks.NotificationManager.Object,
            _mocks.PullRequestsRepository.Object,
            _mocks.CopilotAuth.Object,
            _mocks.Logger.Object
        );
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string body)
    {
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(body)
            });
    }

    #region StartCopilotReviewJobAsync Tests

    [Fact]
    public async Task StartCopilotReviewJobAsync_WithValidRequest_ReturnsJobId()
    {
        var expectedResponse = new AIReviewJobStartedResponseModel { JobId = "job-123" };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(expectedResponse));

        var request = new StartReviewJobRequest { Target = "def hello(): pass", Language = "python" };

        AIReviewJobStartedResponseModel result = await _reviewManager.StartCopilotReviewJobAsync(request);

        Assert.NotNull(result);
        Assert.Equal("job-123", result.JobId);
    }

    [Fact]
    public async Task StartCopilotReviewJobAsync_WithMarkdownFence_InfersLanguageAndStripsContent()
    {
        var expectedResponse = new AIReviewJobStartedResponseModel { JobId = "job-456" };
        string capturedBody = null;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedBody = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse))
            });

        var request = new StartReviewJobRequest { Target = "```py\ndef hello(): pass\n```" };

        AIReviewJobStartedResponseModel result = await _reviewManager.StartCopilotReviewJobAsync(request);

        Assert.NotNull(result);
        Assert.Equal("job-456", result.JobId);
        Assert.Contains("\"language\":\"python\"", capturedBody);
        // Verify the fences are stripped from the sent payload
        Assert.DoesNotContain("```", capturedBody);
        Assert.Contains("def hello(): pass", capturedBody);
    }

    [Fact]
    public async Task StartCopilotReviewJobAsync_WithEmptyTarget_ThrowsArgumentException()
    {
        var request = new StartReviewJobRequest { Target = "", Language = "python" };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _reviewManager.StartCopilotReviewJobAsync(request));
    }

    [Fact]
    public async Task StartCopilotReviewJobAsync_WithNoLanguageAndNoFence_ThrowsArgumentException()
    {
        var request = new StartReviewJobRequest { Target = "def hello(): pass" };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _reviewManager.StartCopilotReviewJobAsync(request));
    }

    [Fact]
    public async Task StartCopilotReviewJobAsync_CopilotReturns500_ThrowsHttpRequestExceptionWithDetails()
    {
        SetupHttpResponse(HttpStatusCode.InternalServerError, "Internal Server Error");

        var request = new StartReviewJobRequest { Target = "def hello(): pass", Language = "python" };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _reviewManager.StartCopilotReviewJobAsync(request));

        Assert.Contains("500", ex.Message);
        Assert.Contains("Internal Server Error", ex.Message);
        Assert.Contains("Copilot service returned", ex.Message);
    }

    [Fact]
    public async Task StartCopilotReviewJobAsync_CopilotReturns422_ThrowsWithStatusAndBody()
    {
        string errorBody = "{\"error\": \"Missing required field: lineNo\"}";
        SetupHttpResponse(HttpStatusCode.UnprocessableEntity, errorBody);

        var request = new StartReviewJobRequest { Target = "def hello(): pass", Language = "python" };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _reviewManager.StartCopilotReviewJobAsync(request));

        Assert.Contains("422", ex.Message);
        Assert.Contains("Missing required field", ex.Message);
    }

    [Fact]
    public async Task StartCopilotReviewJobAsync_WithOptionalFields_IncludesThemInPayload()
    {
        var expectedResponse = new AIReviewJobStartedResponseModel { JobId = "job-789" };
        string capturedBody = null;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedBody = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse))
            });

        var request = new StartReviewJobRequest
        {
            Target = "def hello(): pass",
            Language = "python",
            Base = "def hello(): return None",
            Outline = "## API Overview",
            ExistingComments = new List<ApiViewAgentComment>
            {
                new() { LineNumber = 1, CommentText = "Consider renaming" }
            }
        };

        await _reviewManager.StartCopilotReviewJobAsync(request);

        Assert.Contains("\"base\"", capturedBody);
        Assert.Contains("\"outline\"", capturedBody);
        Assert.Contains("\"comments\"", capturedBody);
    }

    [Fact]
    public async Task StartCopilotReviewJobAsync_WithFencedBase_StripsBaseFences()
    {
        var expectedResponse = new AIReviewJobStartedResponseModel { JobId = "job-base" };
        string capturedBody = null;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedBody = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse))
            });

        var request = new StartReviewJobRequest
        {
            Target = "def hello(): pass",
            Language = "python",
            Base = "```python\ndef old_hello(): pass\n```"
        };

        await _reviewManager.StartCopilotReviewJobAsync(request);

        Assert.Contains("def old_hello(): pass", capturedBody);
        Assert.DoesNotContain("```", capturedBody);
    }

    [Fact]
    public async Task StartCopilotReviewJobAsync_WithoutOptionalFields_OmitsThemFromPayload()
    {
        var expectedResponse = new AIReviewJobStartedResponseModel { JobId = "job-000" };
        string capturedBody = null;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedBody = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse))
            });

        var request = new StartReviewJobRequest { Target = "def hello(): pass", Language = "python" };

        await _reviewManager.StartCopilotReviewJobAsync(request);

        Assert.DoesNotContain("\"base\"", capturedBody);
        Assert.DoesNotContain("\"outline\"", capturedBody);
        Assert.DoesNotContain("\"comments\"", capturedBody);
    }

    [Fact]
    public async Task StartCopilotReviewJobAsync_SendsAuthorizationHeader()
    {
        var expectedResponse = new AIReviewJobStartedResponseModel { JobId = "job-auth" };
        HttpRequestMessage capturedRequest = null;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedRequest = req;
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse))
            });

        var request = new StartReviewJobRequest { Target = "def hello(): pass", Language = "python" };

        await _reviewManager.StartCopilotReviewJobAsync(request);

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
        Assert.Equal("test-token", capturedRequest.Headers.Authorization.Parameter);
    }

    #endregion

    #region GetCopilotReviewJobAsync Tests

    [Fact]
    public async Task GetCopilotReviewJobAsync_WithValidJobId_ReturnsStatus()
    {
        var polledResponse = new AIReviewJobPolledResponseModel { Status = "completed", Details = "done" };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(polledResponse));

        AIReviewJobPolledResponseModel result = await _reviewManager.GetCopilotReviewJobAsync("job-123");

        Assert.NotNull(result);
        Assert.Equal("completed", result.Status);
        Assert.Equal("done", result.Details);
    }

    [Fact]
    public async Task GetCopilotReviewJobAsync_WithComments_ReturnsComments()
    {
        var polledResponse = new AIReviewJobPolledResponseModel
        {
            Status = "Success",
            Comments = new List<AIReviewComment>
            {
                new()
                {
                    LineNo = 1,
                    Comment = "Consider renaming",
                    Suggestion = "Use 'getFoo' instead",
                    Severity = "SHOULD",
                    ConfidenceScore = 0.9f,
                    Source = "analyzer"
                }
            }
        };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(polledResponse));

        AIReviewJobPolledResponseModel result = await _reviewManager.GetCopilotReviewJobAsync("job-with-comments");

        Assert.NotNull(result);
        Assert.Equal("Success", result.Status);
        Assert.Single(result.Comments);
        Assert.Equal("Consider renaming", result.Comments[0].Comment);
        Assert.Equal("SHOULD", result.Comments[0].Severity);
        Assert.Equal(0.9f, result.Comments[0].ConfidenceScore);
    }

    [Fact]
    public async Task GetCopilotReviewJobAsync_WithNullComments_ReturnsNullCommentsList()
    {
        var polledResponse = new AIReviewJobPolledResponseModel { Status = "InProgress", Comments = null };
        SetupHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(polledResponse));

        AIReviewJobPolledResponseModel result = await _reviewManager.GetCopilotReviewJobAsync("job-in-progress");

        Assert.NotNull(result);
        Assert.Equal("InProgress", result.Status);
        Assert.Null(result.Comments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetCopilotReviewJobAsync_WithEmptyJobId_ThrowsArgumentException(string jobId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _reviewManager.GetCopilotReviewJobAsync(jobId));
    }

    [Fact]
    public async Task GetCopilotReviewJobAsync_CallsCorrectUrl()
    {
        HttpRequestMessage capturedRequest = null;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedRequest = req;
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(
                    new AIReviewJobPolledResponseModel { Status = "pending" }))
            });

        await _reviewManager.GetCopilotReviewJobAsync("my-job-id");

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Equal("https://copilot.test/api-review/my-job-id", capturedRequest.RequestUri.ToString());
    }

    #endregion
    
    #region Language inference and stripping (tested indirectly via StartCopilotReviewJobAsync)

    [Theory]
    [InlineData("```cs\npublic class Foo {}\n```", "dotnet", "public class Foo {}")]
    [InlineData("```js\nfunction foo() {}\n```", "typescript", "function foo() {}")]
    [InlineData("```go\nfunc main() {}\n```", "golang", "func main() {}")]
    [InlineData("```java\nclass Foo {}\n```", "java", "class Foo {}")]
    public async Task StartCopilotReviewJobAsync_InfersLanguageAndStripsMarkdownFence(string target, string expectedLanguage, string expectedContent)
    {
        var expectedResponse = new AIReviewJobStartedResponseModel { JobId = "job-lang" };
        string capturedBody = null;

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedBody = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse))
            });

        var request = new StartReviewJobRequest { Target = target };
        await _reviewManager.StartCopilotReviewJobAsync(request);

        Assert.Contains($"\"language\":\"{expectedLanguage}\"", capturedBody);
        Assert.Contains(expectedContent, capturedBody);
        Assert.DoesNotContain("```", capturedBody);
    }

    [Fact]
    public async Task StartCopilotReviewJobAsync_NoFenceNoLanguage_ThrowsArgumentException()
    {
        var request = new StartReviewJobRequest { Target = "plain text with no fence" };
        await Assert.ThrowsAsync<ArgumentException>(() => _reviewManager.StartCopilotReviewJobAsync(request));
    }

    #endregion

    public class MockContainer
    {
        public Mock<IAuthorizationService> AuthorizationService { get; } = new();
        public Mock<ICosmosReviewRepository> ReviewsRepository { get; } = new();
        public Mock<IAPIRevisionsManager> ApiRevisionsManager { get; } = new();
        public Mock<ICommentsManager> CommentManager { get; } = new();
        public Mock<IBlobCodeFileRepository> CodeFileRepository { get; } = new();
        public Mock<ICosmosCommentsRepository> CommentsRepository { get; } = new();
        public Mock<ICosmosAPIRevisionsRepository> ApiRevisionsRepository { get; } = new();
        public Mock<IHubContext<SignalRHub>> SignalRHubContext { get; } = new();
        public TelemetryClient TelemetryClient { get; } = new(new TelemetryConfiguration());
        public Mock<ICodeFileManager> CodeFileManager { get; } = new();
        public Mock<IConfiguration> Configuration { get; } = new();
        public Mock<IHttpClientFactory> HttpClientFactory { get; } = new();
        public Mock<IPollingJobQueueManager> PollingJobQueueManager { get; } = new();
        public Mock<INotificationManager> NotificationManager { get; } = new();
        public Mock<ICosmosPullRequestsRepository> PullRequestsRepository { get; } = new();
        public Mock<ICopilotAuthenticationService> CopilotAuth { get; } = new();
        public Mock<ILogger<ReviewManager>> Logger { get; } = new();
        public IEnumerable<LanguageService> LanguageServices { get; } = new List<LanguageService>();
    }
}
