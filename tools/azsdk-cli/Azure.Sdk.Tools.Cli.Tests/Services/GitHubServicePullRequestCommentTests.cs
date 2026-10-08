// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Moq;
using Moq.Protected;
using Octokit;
using Octokit.Internal;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

public class GitHubServicePullRequestCommentTests
{
    private Mock<HttpMessageHandler> _handler = null!;
    private Mock<IProcessHelper> _process = null!;
    private Mock<IHttpClientFactory> _factory = null!;
    private GitHubService _service = null!;

    [SetUp]
    public void Setup()
    {
        _handler = new(MockBehavior.Strict);
        _process = new(MockBehavior.Strict);
        _factory = new(MockBehavior.Strict);
        _service = new(new TestLogger<GitHubService>(), _process.Object, _factory.Object);
        var connection = new Connection(new ProductHeaderValue("offline-comment-test"), new HttpClientAdapter(() => _handler.Object))
        {
            Credentials = new Credentials("existing-test-identity", AuthenticationType.Bearer)
        };
        // Replace only the fixture transport: no environment changes, credential lookup, or network access.
        typeof(GitConnection).GetField("_gitHubClient", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(_service, new GitHubClient(connection));
    }

    private static HttpResponseMessage Response(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    [Test]
    public async Task CreateComment_OneAuthenticatedIssueCommentPost_WithExactBodyAndPrivateRepo()
    {
        const string body = "<!-- azsdk-abandoned-release-plan:42 -->\nPlan abandoned.";
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, ct) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                    Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo("https://api.github.com/repos/Azure/azure-sdk-for-python-pr/issues/1/comments"));
                    Assert.That(request.Headers.Authorization?.Parameter, Is.EqualTo("existing-test-identity"));
                });
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                Assert.That(json.RootElement.GetProperty("body").GetString(), Is.EqualTo(body));
                return Response("{\"id\":17,\"body\":\"saved\"}", HttpStatusCode.Created);
            });
        var comment = await _service.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python-pr", 1, body, CancellationToken.None);
        Assert.That(comment.Id, Is.EqualTo(17));
        VerifyCalls(1);
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public void CreateComment_ErrorDoesNotRetryPost(HttpStatusCode status)
    {
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Response("{\"message\":\"offline failure\"}", status));
        Assert.CatchAsync<ApiException>(async () =>
            await _service.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, "note", CancellationToken.None));
        VerifyCalls(1);
    }

    [Test]
    public void CreateComment_PreCanceled_DoesNotStartTransport()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () =>
            await _service.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, "note", cts.Token));
        VerifyCalls(0);
    }

    [Test]
    public void CreateComment_CancellationAfterPostStarts_PropagatesWithoutRetry()
    {
        using var cts = new CancellationTokenSource();
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback(() => cts.Cancel()).Returns(pending.Task);
        try
        {
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await _service.CreatePullRequestCommentAsync("Azure", "azure-sdk-for-python", 1, "note", cts.Token));
            VerifyCalls(1);
        }
        finally
        {
            pending.SetResult(Response("{\"id\":17}"));
        }
    }

    [Test]
    public async Task ExistingComments_ReadsEveryPageIncludingPlanMarkerOnLastPage()
    {
        var calls = 0;
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                calls++;
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
                Assert.That(request.Headers.Authorization?.Parameter, Is.EqualTo("existing-test-identity"));
                var response = Response(calls == 1 ? "[{\"id\":1,\"body\":\"other\"}]" :
                    "[{\"id\":2,\"body\":\"<!-- azsdk-abandoned-release-plan:42 -->\"}]");
                if (calls == 1)
                {
                    response.Headers.Add("Link", "<https://api.github.com/repos/Azure/azure-sdk-for-python/issues/1/comments?page=2>; rel=\"next\"");
                }
                else
                {
                    Assert.That(request.RequestUri!.Query, Is.EqualTo("?sort=created&direction=asc&page=2"));
                }
                return Task.FromResult(response);
            });
        var comments = await _service.GetPullRequestIssueCommentsAsync("Azure", "azure-sdk-for-python", 1, CancellationToken.None);
        Assert.That(comments.Select(c => c.Body), Is.EqualTo(new[] { "other", "<!-- azsdk-abandoned-release-plan:42 -->" }));
        VerifyCalls(2);
    }

    private void VerifyCalls(int count)
    {
        _handler.Protected().Verify("SendAsync", Times.Exactly(count), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        Assert.That(_process.Invocations, Is.Empty);
        Assert.That(_factory.Invocations, Is.Empty);
    }
}
