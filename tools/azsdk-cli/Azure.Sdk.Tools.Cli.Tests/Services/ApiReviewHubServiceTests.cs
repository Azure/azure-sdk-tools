// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private Mock<TokenCredential> credentialMock = null!;
    private ApiReviewHubService service = null!;

    [SetUp]
    public void Setup()
    {
        httpClientFactoryMock = new Mock<IHttpClientFactory>();
        azureServiceMock = new Mock<IAzureService>();

        credentialMock = new Mock<TokenCredential>();
        credentialMock
            .Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessToken("mock-token", DateTimeOffset.UtcNow.AddHours(1)));

        azureServiceMock
            .Setup(x => x.GetCredential(It.IsAny<string?>()))
            .Returns(credentialMock.Object);

        service = new ApiReviewHubService(httpClientFactoryMock.Object, azureServiceMock.Object, new TestLogger<ApiReviewHubService>());
    }

    [Test]
    public async Task MarkPackageReleasedAsync_UsesProductionEndpointAndAuthenticatedPayload()
    {
        HttpMethod? method = null;
        Uri? requestUri = null;
        string? authorizationScheme = null;
        string? authorizationParameter = null;
        string? requestBody = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, cancellationToken) =>
            {
                method = request.Method;
                requestUri = request.RequestUri;
                authorizationScheme = request.Headers.Authorization?.Scheme;
                authorizationParameter = request.Headers.Authorization?.Parameter;
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"packageId":"11111111-1111-1111-1111-111111111111","packageVersionId":"22222222-2222-2222-2222-222222222222","packageName":"azure-test","language":"python","version":"1.0.0","releasedApiHash":"api-hash","approvalStatus":"Approved","approvalRecordId":"approval-record-id","appliedInheritanceRule":"prereleaseToPrerelease","isReleased":false,"releasedOn":null}
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var result = await service.MarkPackageReleasedAsync("python", "azure-test", "1.0.0", "api-hash", "tjprescott", CancellationToken.None);

        Assert.That(method, Is.EqualTo(HttpMethod.Post));
        Assert.That(requestUri?.ToString(), Is.EqualTo("https://api-review-hub.azurewebsites.net/api/releases/mark-released"));
        Assert.That(authorizationScheme, Is.EqualTo("Bearer"));
        Assert.That(authorizationParameter, Is.EqualTo("mock-token"));
        Assert.That(requestBody, Does.Contain("\"language\":\"python\""));
        Assert.That(requestBody, Does.Contain("\"packageName\":\"azure-test\""));
        Assert.That(requestBody, Does.Contain("\"version\":\"1.0.0\""));
        Assert.That(requestBody, Does.Contain("\"apiHash\":\"api-hash\""));
        Assert.That(requestBody, Does.Contain("\"repoOwner\":\"tjprescott\""));
        Assert.That(requestBody, Does.Contain("\"dryRun\":false"));
        Assert.That(result.PackageName, Is.EqualTo("azure-test"));
        Assert.That(result.PackageVersionId, Is.EqualTo(Guid.Parse("22222222-2222-2222-2222-222222222222")));
        Assert.That(result.ApprovalStatus, Is.EqualTo("Approved"));
        Assert.That(result.ApprovalRecordId, Is.EqualTo("approval-record-id"));
        Assert.That(result.AppliedInheritanceRule, Is.EqualTo("prereleaseToPrerelease"));
        Assert.That(result.IsReleased, Is.False);
    }

    [Test]
    [TestCase("python", "1.0.0")]
    [TestCase("python", "4.12.0b3")]
    [TestCase("csharp", "4.12.0-beta.3")]
    public async Task GetReleaseGateStatusAsync_UsesExactPackageVersionInQuery(string language, string packageVersion)
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
                      "isApproved": false,
                      "isApiApproved": false,
                      "isPackageNameApproved": true,
                      "reason": "rejected",
                      "details": ["At least one architect has requested changes for this API."],
                      "apiApprovals": []
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var result = await service.GetReleaseGateStatusAsync("https://api-review-hub-test.azurewebsites.net", language, "pkg", packageVersion, "hash", "", CancellationToken.None);

        Assert.That(result.IsReleaseApproved, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(200));
        Assert.That(result.Reason, Is.EqualTo("rejected"));
        Assert.That(result.Details, Is.EqualTo(new[] { "At least one architect has requested changes for this API." }));
        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.RequestUri, Is.Not.Null);
        Assert.That(capturedRequest.RequestUri!.Query, Does.Contain($"version={Uri.EscapeDataString(packageVersion)}"));
    }

    [Test]
    public async Task GetReleaseGateStatusAsync_DeserializesAppliedInheritanceRuleAndApprovalVersion()
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
                      "isApproved": true,
                      "isApiApproved": true,
                      "isPackageNameApproved": true,
                      "reason": "approved",
                      "appliedInheritanceRule": "prereleaseToStable",
                      "details": [],
                      "apiApprovals": [
                        {
                          "id": "approval-record-id",
                          "apiHash": "hash-1",
                          "version": "4.12.0b3",
                          "status": "approved",
                          "pullRequestUrl": "https://github.com/Azure/azure-sdk-for-python/pull/1",
                          "lastUpdatedBy": "octocat",
                          "lastUpdatedOn": "2026-07-27T00:00:00Z"
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var result = await service.GetReleaseGateStatusAsync("https://api-review-hub-test.azurewebsites.net", "python", "pkg", "4.12.0b3", "hash", "", CancellationToken.None);

        Assert.That(result.AppliedInheritanceRule, Is.EqualTo("prereleaseToStable"));
        Assert.That(result.IsApiApproved, Is.True);
        Assert.That(result.IsPackageNameApproved, Is.True);
        Assert.That(result.ApiApprovals, Is.Not.Null);
        Assert.That(result.ApiApprovals!.Count, Is.EqualTo(1));
        Assert.That(result.ApiApprovals[0].Id, Is.EqualTo("approval-record-id"));
        Assert.That(result.ApiApprovals[0].Version, Is.EqualTo("4.12.0b3"));
        Assert.That(result.ApiApprovals[0].ApiHash, Is.EqualTo("hash-1"));
        Assert.That(result.ApiApprovals[0].Status, Is.EqualTo("approved"));
        Assert.That(result.ApiApprovals[0].PullRequestUrl, Is.EqualTo("https://github.com/Azure/azure-sdk-for-python/pull/1"));
        Assert.That(result.ApiApprovals[0].LastUpdatedBy, Is.EqualTo("octocat"));
        Assert.That(result.ApiApprovals[0].LastUpdatedOn, Is.EqualTo("2026-07-27T00:00:00Z"));
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
                      "isApiApproved": false,
                      "isPackageNameApproved": true,
                      "reason": "pending",
                      "details": ["Approval is still pending."],
                      "apiApprovals": []
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var result = await service.GetReleaseGateStatusAsync("https://api-review-hub-test.azurewebsites.net", "python", "pkg", "1.0.0", "hash", "", CancellationToken.None);

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
                      "isApiApproved": true,
                      "isPackageNameApproved": true,
                      "reason": "approved",
                      "details": [],
                      "apiApprovals": []
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        _ = await service.GetReleaseGateStatusAsync("https://api-review-hub-test.azurewebsites.net", "python", "pkg", "1.0.0", "hash", "", CancellationToken.None);

        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.Headers.Authorization, Is.Not.Null);
        Assert.That(capturedRequest.Headers.Authorization!.Scheme, Is.EqualTo("Bearer"));
        Assert.That(capturedRequest.Headers.Authorization.Parameter, Is.EqualTo("mock-token"));
        Assert.That(capturedRequest.Headers.Accept.Any(h => h.MediaType == "application/json"), Is.True);
    }

    [Test]
    public async Task GetReleaseGateStatusAsync_HandlesMissingApiApprovalsProperty()
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
                      "isApiApproved": false,
                      "isPackageNameApproved": false,
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

        var result = await service.GetReleaseGateStatusAsync("https://api-review-hub-test.azurewebsites.net", "python", "pkg", "1.0.0", "hash", "", CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(200));
        Assert.That(result.Reason, Is.EqualTo("repositoryNotSupported"));
        Assert.That(result.ApiApprovals, Is.Not.Null);
        Assert.That(result.ApiApprovals, Is.Empty);
    }

    [TestCase(true, true, true, "approved")]
    [TestCase(true, true, true, "reviewNotRequired")]
    [TestCase(false, true, false, "packageNameNotApproved")]
    [TestCase(false, false, true, "missingApproval")]
    [TestCase(false, false, true, "rejected")]
    [TestCase(false, false, false, "packageNameNotApproved")]
    [TestCase(false, false, false, "repositoryNotSupported")]
    [TestCase(false, false, false, "unknownPackage")]
    public async Task GetReleaseGateStatusAsync_DeserializesReleaseDecision(
        bool isApproved, bool isApiApproved, bool isPackageNameApproved, string reason)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    isApproved,
                    isApiApproved,
                    isPackageNameApproved,
                    reason,
                    details = new[] { "Additional API failure context" },
                    apiApprovals = Array.Empty<object>()
                }), Encoding.UTF8, "application/json")
            });
        httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(mockHandler.Object));

        var result = await service.GetReleaseGateStatusAsync(
            "https://api-review-hub-test.azurewebsites.net", "python", "pkg", "1.0.0", "hash", "", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsReleaseApproved, Is.EqualTo(isApproved));
            Assert.That(result.IsApiApproved, Is.EqualTo(isApiApproved));
            Assert.That(result.IsPackageNameApproved, Is.EqualTo(isPackageNameApproved));
            Assert.That(result.Reason, Is.EqualTo(reason));
            Assert.That(result.Details, Is.EqualTo(new[] { "Additional API failure context" }));
        });
    }

    [TestCase("isApiApproved")]
    [TestCase("isPackageNameApproved")]
    public void GetReleaseGateStatusAsync_RejectsMissingApprovalFields(string missingField)
    {
        var payload = new JsonObject
        {
            ["isApproved"] = true,
            ["isApiApproved"] = true,
            ["isPackageNameApproved"] = true,
            ["reason"] = "reviewNotRequired"
        };
        payload.Remove(missingField);
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
            });
        httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(mockHandler.Object));

        var exception = Assert.ThrowsAsync<JsonException>(() => service.GetReleaseGateStatusAsync(
            "https://api-review-hub-test.azurewebsites.net", "python", "pkg", "1.0.0", "hash", "", CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain(missingField));
    }

    [Test]
    public void GetReleaseGateStatusAsync_WithDisallowedHost_ThrowsInvalidOperationException()
    {
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GetReleaseGateStatusAsync("https://api-review-hub.evil.example", "python", "pkg", "1.0.0", "hash", "", CancellationToken.None));

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
            PackageType = "client",
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

    [Test]
    public async Task RequestReviewPullRequestAsync_RefreshesAuthorizationNearExpiry()
    {
        credentialMock
            .SetupSequence(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessToken("request-token", DateTimeOffset.UtcNow.AddMinutes(1)))
            .ReturnsAsync(new AccessToken("poll-token-1", DateTimeOffset.UtcNow.AddHours(1)))
            .ReturnsAsync(new AccessToken("poll-token-2", DateTimeOffset.UtcNow.AddHours(1)));

        var authorizationParameters = new List<string?>();
        var pollCount = 0;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) =>
                authorizationParameters.Add(request.Headers.Authorization?.Parameter))
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                if (request.Method == HttpMethod.Post)
                {
                    return new HttpResponseMessage(HttpStatusCode.Accepted)
                    {
                        Content = new StringContent("""{"operationId":"op-refresh","status":"accepted"}""", Encoding.UTF8, "application/json")
                    };
                }

                pollCount++;
                var status = pollCount == 1 ? "running" : "succeeded";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($$"""{"operationId":"op-refresh","status":"{{status}}"}""", Encoding.UTF8, "application/json")
                };
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var request = new ReviewPullRequestCreationRequest
        {
            Language = "python",
            PackageName = "pkg",
            PackageType = "client",
            TargetBranch = new GitBranchReference
            {
                Owner = "Azure",
                Repo = "azure-sdk-for-python",
                Name = "main"
            }
        };

        var result = await service.RequestReviewPullRequestAsync(
            request,
            "https://api-review-hub-test.azurewebsites.net",
            waitForCompletion: true,
            pollInterval: TimeSpan.Zero,
            CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo("succeeded"));
        Assert.That(authorizationParameters, Is.EqualTo(new[] { "request-token", "poll-token-1", "poll-token-1" }));
        credentialMock.Verify(
            x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Test]
    public async Task RequestReviewPullRequestAsync_WithoutBaseTag_SendsEmptyString()
    {
        string? requestBody = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request => request.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, cancellationToken) =>
                requestBody = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(
                    """
                    {
                      "operationId": "op-accepted",
                      "status": "accepted"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var request = new ReviewPullRequestCreationRequest
        {
            Language = "python",
            PackageName = "pkg",
            PackageType = "client",
            TargetBranch = new GitBranchReference
            {
                Owner = "Azure",
                Repo = "azure-sdk-for-python",
                Name = "main"
            }
        };

        await service.RequestReviewPullRequestAsync(
            request,
            "https://api-review-hub-test.azurewebsites.net",
            waitForCompletion: false,
            pollInterval: TimeSpan.Zero,
            CancellationToken.None);

        Assert.That(requestBody, Does.Contain("\"baseTag\":\"\""));
        Assert.That(requestBody, Does.Contain("\"packageType\":\"client\""));
    }

    [Test]
    public async Task RequestReviewPullRequestAsync_WhenReviewPullRequestAlreadyExists_ReturnsExpectedStatus()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request => request.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    """
                    {
                      "error": {
                        "code": "reviewPullRequestAlreadyExists",
                                                "message": "An API Review Hub review PR already exists for pkg.",
                                                "reviewPullRequest": {
                                                    "url": "https://github.com/Azure/azure-sdk-for-python/pull/13"
                                                }
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var request = new ReviewPullRequestCreationRequest
        {
            Language = "python",
            PackageName = "pkg",
            PackageType = "client",
            BaseTag = "v1.0.0",
            TargetBranch = new GitBranchReference
            {
                Owner = "Azure",
                Repo = "azure-sdk-for-python",
                Name = "main"
            }
        };

        var result = await service.RequestReviewPullRequestAsync(
            request,
            "https://api-review-hub-test.azurewebsites.net",
            waitForCompletion: true,
            pollInterval: TimeSpan.Zero,
            CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo("succeeded"));
        Assert.That(result.PackageName, Is.EqualTo("pkg"));
        Assert.That(result.Message, Is.EqualTo("An API Review Hub review PR already exists for pkg."));
        Assert.That(result.ReviewPullRequest, Is.Not.Null);
        Assert.That(result.ReviewPullRequest!.Value.GetProperty("url").GetString(), Is.EqualTo("https://github.com/Azure/azure-sdk-for-python/pull/13"));
    }

    [Test]
    public async Task RequestReviewPullRequestAsync_WhenConflictIsNotExpected_Throws()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request => request.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    """
                    {
                      "error": {
                        "code": "operationConflict",
                        "message": "Another operation is in progress."
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        var request = new ReviewPullRequestCreationRequest
        {
            Language = "python",
            PackageName = "pkg",
            PackageType = "client",
            BaseTag = "v1.0.0",
            TargetBranch = new GitBranchReference
            {
                Owner = "Azure",
                Repo = "azure-sdk-for-python",
                Name = "main"
            }
        };

        HttpRequestException? exception = null;
        try
        {
            await service.RequestReviewPullRequestAsync(
                request,
                "https://api-review-hub-test.azurewebsites.net",
                waitForCompletion: true,
                pollInterval: TimeSpan.Zero,
                CancellationToken.None);
            Assert.Fail("Expected HttpRequestException was not thrown.");
        }
        catch (HttpRequestException ex)
        {
            exception = ex;
        }

        Assert.That(exception!.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(exception.Message, Does.Contain("operationConflict"));
    }

    [Test]
    public async Task GetReleaseGateStatusAsync_IncludesRepoOwnerInQuery_WhenProvided()
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
                      "isApiApproved": true,
                      "isPackageNameApproved": true,
                      "reason": "approved",
                      "details": [],
                      "apiApprovals": []
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        httpClientFactoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(mockHandler.Object));

        _ = await service.GetReleaseGateStatusAsync(
            "https://api-review-hub-test.azurewebsites.net",
            "python",
            "pkg",
            "1.0.0",
            "hash",
            "Contoso",
            CancellationToken.None);

        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.RequestUri, Is.Not.Null);
        Assert.That(capturedRequest.RequestUri!.Query, Does.Contain("repoOwner=Contoso"));
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
