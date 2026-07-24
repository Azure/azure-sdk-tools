// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Net;
using System.Text;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Models.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.ApiReviewHub;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using Moq.Protected;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class ApiReviewHubServiceTests
{
    private Mock<IHttpClientFactory> httpClientFactoryMock = null!;
    private Mock<IAzureService> azureServiceMock = null!;
    private ApiReviewHubService service = null!;

    [SetUp]
    public void Setup()
    {
        httpClientFactoryMock = new Mock<IHttpClientFactory>();
        azureServiceMock = new Mock<IAzureService>();

        var credentialMock = new Mock<TokenCredential>();
        credentialMock
            .Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessToken("mock-token", DateTimeOffset.UtcNow.AddHours(1)));

        azureServiceMock
            .Setup(x => x.GetCredential(It.IsAny<string?>()))
            .Returns(credentialMock.Object);

        service = new ApiReviewHubService(httpClientFactoryMock.Object, azureServiceMock.Object, new TestLogger<ApiReviewHubService>());
    }

    [Test]
    public async Task GetReleaseGateStatusAsync_DeserializesArrayDetails()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                                            "isApproved": false,
                      "reason": "rejected",
                                            "details": ["At least one architect has requested changes for this API."],
                      "approvals": []
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var result = await service.GetReleaseGateStatusAsync("https://api-review-hub-test.azurewebsites.net", "python", "pkg", "1.0.0", "hash", CancellationToken.None);

        Assert.That(result.IsApproved, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(200));
        Assert.That(result.Reason, Is.EqualTo("rejected"));
        Assert.That(result.Details, Is.EqualTo(new[] { "At least one architect has requested changes for this API." }));
    }

    [Test]
    public async Task GetReleaseGateStatusAsync_PreservesActualSuccessStatusCode()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(
                    """
                    {
                      "isApproved": false,
                      "reason": "pending",
                      "details": ["Approval is still pending."],
                      "approvals": []
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var result = await service.GetReleaseGateStatusAsync("https://api-review-hub-test.azurewebsites.net", "python", "pkg", "1.0.0", "hash", CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(202));
    }

    [Test]
    public async Task GetReleaseGateStatusAsync_SetsAuthorizationHeaderPerRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "isApproved": true,
                      "reason": "approved",
                      "details": [],
                      "approvals": []
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        _ = await service.GetReleaseGateStatusAsync("https://api-review-hub-test.azurewebsites.net", "python", "pkg", "1.0.0", "hash", CancellationToken.None);

        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.Headers.Authorization, Is.Not.Null);
        Assert.That(capturedRequest.Headers.Authorization!.Scheme, Is.EqualTo("Bearer"));
        Assert.That(capturedRequest.Headers.Authorization.Parameter, Is.EqualTo("mock-token"));
        Assert.That(capturedRequest.Headers.Accept.Any(h => h.MediaType == "application/json"), Is.True);
    }

    [Test]
    public async Task GetReleaseGateStatusAsync_HandlesMissingApprovalsProperty()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "isApproved": false,
                      "reason": "repositoryNotSupported",
                      "details": ["Repository is not onboarded in API Review Hub."]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var result = await service.GetReleaseGateStatusAsync("https://api-review-hub-test.azurewebsites.net", "python", "pkg", "1.0.0", "hash", CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(200));
        Assert.That(result.Reason, Is.EqualTo("repositoryNotSupported"));
        Assert.That(result.Approvals, Is.Not.Null);
        Assert.That(result.Approvals, Is.Empty);
    }

    [Test]
    public void GetReleaseGateStatusAsync_WithDisallowedHost_ThrowsInvalidOperationException()
    {
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GetReleaseGateStatusAsync("https://api-review-hub.evil.example", "python", "pkg", "1.0.0", "hash", CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("endpoint host is not allowed"));
    }

    [Test]
    public void RequestReviewPullRequestAsync_WhenOperationNeverCompletes_TimesOut()
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request => request.Method == HttpMethod.Post && request.RequestUri != null && request.RequestUri.AbsolutePath == "/api/review-prs"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(
                    """
                    {
                      "operationId": "op-timeout",
                      "status": "accepted"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request => request.Method == HttpMethod.Get && request.RequestUri != null && request.RequestUri.AbsolutePath == "/api/operations/op-timeout"),
                ItExpr.IsAny<CancellationToken>())
                        .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                        {
                                Content = new StringContent(
                                        """
                                        {
                                            "operationId": "op-timeout",
                                            "status": "running"
                                        }
                                        """,
                                        Encoding.UTF8,
                                        "application/json")
                        });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var timeProvider = new SteppingTimeProvider(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        var timedService = new ApiReviewHubService(
            httpClientFactoryMock.Object,
            azureServiceMock.Object,
            new TestLogger<ApiReviewHubService>(),
            timeProvider,
            TimeSpan.FromSeconds(5));

        var request = new ReviewPullRequestCreationRequest
        {
            Language = "python",
            PackageName = "pkg",
            BaseTag = "v1.0.0",
            TargetBranch = new GitBranchReference
            {
                Owner = "Azure",
                Repo = "azure-sdk-for-python",
                Name = "main"
            }
        };

        var exception = Assert.ThrowsAsync<TimeoutException>(async () =>
            await timedService.RequestReviewPullRequestAsync(request, "https://api-review-hub-test.azurewebsites.net", waitForCompletion: true, pollInterval: TimeSpan.Zero, CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("op-timeout"));
        Assert.That(exception.Message, Does.Contain("timed out"));
    }

    private sealed class SteppingTimeProvider(DateTimeOffset initial, TimeSpan step) : TimeProvider
    {
        private DateTimeOffset _current = initial;
        private readonly TimeSpan _step = step;

        public override DateTimeOffset GetUtcNow()
        {
            var value = _current;
            _current = _current.Add(_step);
            return value;
        }
    }
}
