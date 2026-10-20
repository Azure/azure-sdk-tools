// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using Moq.Protected;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
[NonParallelizable]
public class GitHubServiceApprovalTests
{
    private string? _originalGitHubToken;
    private string? _originalGitHubPat;
    private Mock<IProcessHelper> _processHelper = null!;
    private Mock<IHttpClientFactory> _httpClientFactory = null!;
    private Mock<HttpMessageHandler> _handler = null!;
    private HttpClient _httpClient = null!;
    private GitHubService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _originalGitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        _originalGitHubPat = Environment.GetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN");
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "unit-test-token", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN", null, EnvironmentVariableTarget.Process);

        _processHelper = new Mock<IProcessHelper>(MockBehavior.Strict);
        _handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _httpClient = new HttpClient(_handler.Object, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        _httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        _httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        _service = new GitHubService(new TestLogger<GitHubService>(), _processHelper.Object, _httpClientFactory.Object);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            _httpClient.Dispose();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", _originalGitHubToken, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN", _originalGitHubPat, EnvironmentVariableTarget.Process);
        }
    }

    [TestCase("APPROVED", true)]
    [TestCase("REVIEW_REQUIRED", false)]
    [TestCase("CHANGES_REQUESTED", false)]
    public async Task IsPullRequestApprovedAsync_ReturnsCurrentAggregateDecision(string decision, bool expected)
    {
        using var response = RespondWith(ApprovalResponse(decision));

        var approved = await _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, CancellationToken.None);

        Assert.That(approved, Is.EqualTo(expected));
        VerifyHttpCalls(Times.Once());
    }

    [TestCase("primary-test-token", "fallback-test-token", "primary-test-token")]
    [TestCase(null, "fallback-test-token", "fallback-test-token")]
    public async Task IsPullRequestApprovedAsync_UsesExistingAuthenticatedGraphQLClientAndQueryVariables(
        string? token, string? pat, string expectedToken)
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", token, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN", pat, EnvironmentVariableTarget.Process);
        using var response = CreateResponse(ApprovalResponse("APPROVED"));
        string? requestBody = null;
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, ct) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                    Assert.That(request.RequestUri, Is.EqualTo(new Uri("https://api.github.com/graphql")));
                    Assert.That(request.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"));
                    Assert.That(request.Headers.Authorization?.Parameter, Is.EqualTo(expectedToken));
                    Assert.That(request.Headers.UserAgent.ToString(), Is.EqualTo("AzureSDKDevToolsMCP/1.0"));
                    Assert.That(request.Content?.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
                });
                requestBody = await request.Content!.ReadAsStringAsync(ct);
                return response;
            });

        var approved = await _service.IsPullRequestApprovedAsync("Contoso", "sdk-review-tests", 17, CancellationToken.None);

        Assert.That(approved, Is.True);
        Assert.That(requestBody, Is.Not.Null);
        using var document = JsonDocument.Parse(requestBody!);
        var query = document.RootElement.GetProperty("query").GetString();
        var variables = document.RootElement.GetProperty("variables");
        Assert.Multiple(() =>
        {
            Assert.That(query, Does.Contain("$owner: String!").And.Contain("$repo: String!").And.Contain("$number: Int!"));
            Assert.That(query, Does.Contain("repository(owner: $owner, name: $repo)"));
            Assert.That(query, Does.Contain("pullRequest(number: $number)"));
            Assert.That(query, Does.Match(@"\bstate\b").And.Match(@"\breviewDecision\b"));
            Assert.That(query, Does.Not.Contain("reviews").IgnoreCase);
            Assert.That(query, Does.Not.Contain("Contoso").And.Not.Contain("sdk-review-tests"));
            Assert.That(variables.GetProperty("owner").GetString(), Is.EqualTo("Contoso"));
            Assert.That(variables.GetProperty("repo").GetString(), Is.EqualTo("sdk-review-tests"));
            Assert.That(variables.GetProperty("number").GetInt32(), Is.EqualTo(17));
            Assert.That(variables.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "owner", "repo", "number" }));
            Assert.That(Environment.GetEnvironmentVariable("GITHUB_TOKEN"), Is.EqualTo(expectedToken));
        });
        _httpClientFactory.Verify(f => f.CreateClient(string.Empty), Times.Once);
        _processHelper.Verify(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyHttpCalls(Times.Once());
    }

    [TestCase("REVIEW_REQUIRED")]
    [TestCase("CHANGES_REQUESTED")]
    public async Task IsPullRequestApprovedAsync_RechecksDecision_InsteadOfRetainingHistoricalApproval(string currentDecision)
    {
        using var approvedResponse = CreateResponse(ApprovalResponse("APPROVED"));
        using var currentResponse = CreateResponse(PullRequestResponse(JsonSerializer.Serialize(new
        {
            state = "OPEN",
            reviewDecision = currentDecision,
            reviews = new { nodes = new[] { new { state = "APPROVED" } } }
        })));
        _handler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(approvedResponse)
            .ReturnsAsync(currentResponse);

        Assert.That(await _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, CancellationToken.None), Is.True);
        Assert.That(await _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, CancellationToken.None), Is.False);
        VerifyHttpCalls(Times.Exactly(2));
    }

    [TestCaseSource(nameof(UnverifiableApprovalResponses))]
    public void IsPullRequestApprovedAsync_UnverifiableResponse_ThrowsInsteadOfReturningABoolean(string json)
    {
        using var response = RespondWith(json);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, CancellationToken.None));
        VerifyHttpCalls(Times.Once());
    }

    private static IEnumerable<TestCaseData> UnverifiableApprovalResponses()
    {
        yield return InvalidResponse("MissingData", "{}");
        yield return InvalidResponse("MissingRepository", """{"data":{}}""");
        yield return InvalidResponse("MissingPullRequest", """{"data":{"repository":{}}}""");
        yield return InvalidResponse("MissingState", PullRequestResponse("""{"reviewDecision":"APPROVED"}"""));
        yield return InvalidResponse("MissingDecision", PullRequestResponse("""{"state":"OPEN"}"""));

        foreach (var (shape, value) in new[]
        {
            ("Null", "null"), ("Array", "[]"), ("Boolean", "true"), ("Number", "17"), ("String", "\"unexpected\"")
        })
        {
            yield return InvalidResponse($"Root{shape}", value);
            yield return InvalidResponse($"Data{shape}", "{\"data\":" + value + "}");
            yield return InvalidResponse($"Repository{shape}", "{\"data\":{\"repository\":" + value + "}}");
            yield return InvalidResponse($"PullRequest{shape}", PullRequestResponse(value));
            yield return InvalidResponse($"State{shape}", PullRequestResponse("{\"state\":" + value + ",\"reviewDecision\":\"APPROVED\"}"));
            yield return InvalidResponse($"Decision{shape}", PullRequestResponse("{\"state\":\"OPEN\",\"reviewDecision\":" + value + "}"));
        }
        yield return InvalidResponse("StateObject", PullRequestResponse("""{"state":{},"reviewDecision":"APPROVED"}"""));
        yield return InvalidResponse("DecisionObject", PullRequestResponse("""{"state":"OPEN","reviewDecision":{}}"""));

        foreach (var state in new[] { "CLOSED", "MERGED", "open", "" })
        {
            yield return InvalidResponse($"NonOpenState_{state}", PullRequestResponse(JsonSerializer.Serialize(new { state, reviewDecision = "APPROVED" })));
        }
        foreach (var decision in new[] { "approved", "", "DISMISSED", "COMMENTED", "PENDING" })
        {
            yield return InvalidResponse($"UnknownDecision_{decision}", ApprovalResponse(decision));
        }
    }

    private static TestCaseData InvalidResponse(string name, string json) =>
        new TestCaseData(json).SetName($"IsPullRequestApprovedAsync_Rejects_{name}");

    [TestCase("APPROVED")]
    [TestCase("REVIEW_REQUIRED")]
    [TestCase("CHANGES_REQUESTED")]
    public void IsPullRequestApprovedAsync_PartialGraphQLError_RejectsEvenUsableDecisionData(string decision)
    {
        var json = "{\"errors\":[{\"message\":\"Approval lookup denied\"}]," + ApprovalResponse(decision)[1..];
        using var response = RespondWith(json);

        var exception = Assert.ThrowsAsync<Exception>(async () =>
            await _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, CancellationToken.None));

        Assert.That(exception!.Message, Is.EqualTo("GitHub GraphQL error: Approval lookup denied"));
    }

    [TestCase("""{"errors":[{"message":"Resource not accessible"}]}""", "Resource not accessible")]
    [TestCase("""{"data":null,"errors":[{"message":"Not found"}]}""", "Not found")]
    [TestCase("""{"errors":[{}]}""", "unknown error")]
    [TestCase("""{"errors":[null]}""", "unknown error")]
    [TestCase("""{"errors":["denied"]}""", "unknown error")]
    [TestCase("""{"errors":[{"message":null}]}""", "unknown error")]
    public void IsPullRequestApprovedAsync_GraphQLErrors_ThrowIncludingMissingErrorMessages(string json, string message)
    {
        using var response = RespondWith(json);

        var exception = Assert.ThrowsAsync<Exception>(async () =>
            await _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, CancellationToken.None));

        Assert.That(exception!.Message, Is.EqualTo($"GitHub GraphQL error: {message}"));
    }

    [TestCase("""{"errors":[{"message":17}]}""")]
    [TestCase("""{"errors":[{"message":true}]}""")]
    [TestCase("""{"errors":[{"message":[]}]}""")]
    [TestCase("""{"errors":[{"message":{}}]}""")]
    public void IsPullRequestApprovedAsync_InvalidGraphQLErrorMessageType_StillThrows(string json)
    {
        using var response = RespondWith(json);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, CancellationToken.None));
    }

    [TestCase("APPROVED", true)]
    [TestCase("REVIEW_REQUIRED", false)]
    public async Task IsPullRequestApprovedAsync_EmptyGraphQLErrors_PreservesDecision(string decision, bool expected)
    {
        using var response = RespondWith("{\"errors\":[]," + ApprovalResponse(decision)[1..]);

        var approved = await _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, CancellationToken.None);

        Assert.That(approved, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("not JSON")]
    [TestCase("{")]
    public void IsPullRequestApprovedAsync_InvalidJson_ThrowsInsteadOfAssumingUnapproved(string json)
    {
        using var response = RespondWith(json);

        Assert.CatchAsync<JsonException>(async () =>
            await _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, CancellationToken.None));
    }

    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public void IsPullRequestApprovedAsync_HttpFailure_ThrowsEvenWithApprovedResponseBody(HttpStatusCode status)
    {
        using var response = RespondWith(ApprovalResponse("APPROVED"), status);

        var exception = Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, CancellationToken.None));

        Assert.That(exception!.StatusCode, Is.EqualTo(status));
    }

    [Test]
    public void IsPullRequestApprovedAsync_TransportFailure_IsNotTreatedAsUnapproved()
    {
        var failure = new HttpRequestException("Simulated transport failure");
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(failure);

        var exception = Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, CancellationToken.None));

        Assert.That(exception, Is.SameAs(failure));
    }

    [Test]
    public void IsPullRequestApprovedAsync_AuthenticationFailure_DoesNotSendHttp()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN", null, EnvironmentVariableTarget.Process);
        _processHelper.Setup(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult { ExitCode = 1 });

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("Failed to get GitHub auth token."));
        _processHelper.Verify(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        VerifyHttpCalls(Times.Never());
    }

    [Test]
    public void IsPullRequestApprovedAsync_CancelledBeforeIo_DoesNotAuthenticateOrCreateClient()
    {
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN", null, EnvironmentVariableTarget.Process);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = Assert.CatchAsync<OperationCanceledException>(async () =>
            await _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, cancellation.Token));

        Assert.That(exception!.CancellationToken, Is.EqualTo(cancellation.Token));
        _httpClientFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
        _processHelper.Verify(p => p.Run(It.IsAny<ProcessOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyHttpCalls(Times.Never());
    }

    [Test]
    public void IsPullRequestApprovedAsync_CancelledDuringHttp_ForwardsCancellationWithoutWaiting()
    {
        using var cancellation = new CancellationTokenSource();
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken requestToken = default;
        CancellationTokenRegistration registration = default;
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((_, ct) =>
            {
                requestToken = ct;
                registration = ct.Register(() => completion.TrySetCanceled(ct));
                return completion.Task;
            });

        try
        {
            var pending = _service.IsPullRequestApprovedAsync("Azure", "azure-sdk-for-net", 42, cancellation.Token);
            Assert.That(requestToken.CanBeCanceled, Is.True);
            Assert.That(pending.IsCompleted, Is.False);

            cancellation.Cancel();

            // Assert propagation before awaiting, so a missing token fails instead of hanging the test.
            Assert.That(requestToken.IsCancellationRequested, Is.True);
            Assert.That(completion.Task.IsCanceled, Is.True);
            Assert.CatchAsync<OperationCanceledException>(async () => await pending);
            VerifyHttpCalls(Times.Once());
        }
        finally
        {
            registration.Dispose();
            completion.TrySetCanceled();
        }
    }

    private static string PullRequestResponse(string pullRequest) =>
        "{\"data\":{\"repository\":{\"pullRequest\":" + pullRequest + "}}}";

    private static string ApprovalResponse(string decision) =>
        PullRequestResponse(JsonSerializer.Serialize(new { state = "OPEN", reviewDecision = decision }));

    private static HttpResponseMessage CreateResponse(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private HttpResponseMessage RespondWith(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = CreateResponse(json, status);
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
        return response;
    }

    private void VerifyHttpCalls(Times times) =>
        _handler.Protected().Verify("SendAsync", times, ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
}
