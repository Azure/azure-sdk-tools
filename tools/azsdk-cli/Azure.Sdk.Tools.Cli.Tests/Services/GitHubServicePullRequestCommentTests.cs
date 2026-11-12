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
        var anonymousConnection = new Connection(new ProductHeaderValue("offline-anonymous-test"), new HttpClientAdapter(() => _handler.Object));
        typeof(GitConnection).GetField("_anonymousGitHubClient", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(_service, new GitHubClient(anonymousConnection));
    }

    private static HttpResponseMessage Response(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    [TestCase("commit")]
    [TestCase("review")]
    [TestCase("comment")]
    [TestCase("review comment")]
    [TestCase("none")]
    public async Task LatestActivity_UsesQualifyingTimestampsInsteadOfPullRequestUpdatedAt(string source)
    {
        const string oldTime = "2026-08-25T00:00:00Z";
        const string recentTime = "2026-10-26T12:00:00Z";
        var requests = new List<string>();
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
                var path = request.RequestUri!.AbsolutePath;
                requests.Add(path);
                var timestamp = path.EndsWith("/commits") && source == "commit"
                    || path.EndsWith("/reviews") && source == "review"
                    || path.Contains("/issues/") && source == "comment"
                    || path.Contains("/pulls/") && path.EndsWith("/comments") && source == "review comment"
                        ? recentTime : oldTime;
                var json = path.EndsWith("/commits")
                    ? JsonSerializer.Serialize(new[] { new { sha = "commit", commit = new { committer = new { name = "Author", email = "author@example.com", date = timestamp } } } })
                    : path.EndsWith("/reviews")
                        ? JsonSerializer.Serialize(new[] { new { id = 1, state = "CHANGES_REQUESTED", submitted_at = timestamp } })
                        : JsonSerializer.Serialize(new object[]
                        {
                            new { id = 1, user = new { login = "reviewer", type = "User" }, created_at = oldTime, updated_at = timestamp },
                            new { id = 2, user = new { login = "checks[bot]", type = "Bot" }, created_at = "2026-11-09T00:00:00Z", updated_at = "2026-11-09T00:00:00Z" }
                        });
                return Task.FromResult(Response(json));
            });

        var activity = await _service.GetPullRequestLatestActivityAsync("Azure", "azure-sdk-for-python", 1, CancellationToken.None);

        Assert.That(activity.Timestamp, Is.EqualTo(DateTimeOffset.Parse(source == "none" ? oldTime : recentTime)));
        Assert.That(activity.Source, Is.EqualTo(source == "none" ? "commit" : source));
        Assert.That(requests, Has.Count.EqualTo(4));
        Assert.That(requests, Has.None.Matches<string>(path => path.EndsWith("/checks") || path.EndsWith("/statuses")));
    }

    [Test]
    public async Task LatestActivity_FollowsCommentPaginationAndIgnoresPendingReviews()
    {
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                var path = request.RequestUri!.AbsolutePath;
                if (path.EndsWith("/commits"))
                {
                    return Task.FromResult(Response("""[{"commit":{"committer":{"date":"2026-08-25T00:00:00Z"}}}]"""));
                }
                if (path.EndsWith("/reviews"))
                {
                    return Task.FromResult(Response("""[{"id":1,"state":"PENDING"}]"""));
                }
                if (path.Contains("/issues/"))
                {
                    var secondPage = request.RequestUri.Query.Contains("page=2");
                    var response = Response(secondPage
                        ? """[{"id":2,"user":{"login":"reviewer","type":"User"},"created_at":"2026-10-26T00:00:00Z","updated_at":"2026-10-26T00:00:00Z"}]"""
                        : """[{"id":1,"user":{"login":"checks[bot]","type":"Bot"},"created_at":"2026-11-09T00:00:00Z","updated_at":"2026-11-09T00:00:00Z"}]""");
                    if (!secondPage)
                    {
                        response.Headers.Add("Link", "<https://api.github.com/repos/Azure/azure-sdk-for-python/issues/1/comments?page=2>; rel=\"next\"");
                    }
                    return Task.FromResult(response);
                }
                return Task.FromResult(Response("[]"));
            });

        var activity = await _service.GetPullRequestLatestActivityAsync("Azure", "azure-sdk-for-python", 1, CancellationToken.None);

        Assert.That(activity.Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-10-26T00:00:00Z")));
        Assert.That(activity.Source, Is.EqualTo("comment"));
        VerifyCalls(5);
    }

    [Test]
    public void LatestActivity_MissingCommitTimestampDoesNotAuthorizeCleanup()
    {
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Response("""[{"commit":{}}]"""));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.GetPullRequestLatestActivityAsync("Azure", "azure-sdk-for-python", 1, CancellationToken.None));

        VerifyCalls(1);
    }

    [Test]
    public void LatestActivity_PreCanceledDoesNotReadHistory()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await _service.GetPullRequestLatestActivityAsync("Azure", "azure-sdk-for-python", 1, cancellation.Token));

        VerifyCalls(0);
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public void LatestActivity_FailedHistoryReadDoesNotReturnInactivity(HttpStatusCode status)
    {
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Response("{\"message\":\"offline failure\"}", status));

        Assert.CatchAsync<ApiException>(async () =>
            await _service.GetPullRequestLatestActivityAsync("Azure", "azure-sdk-for-python", 1, CancellationToken.None));

        VerifyCalls(1);
    }

    [TestCase(0)]
    [TestCase(250)]
    public void LatestActivity_EmptyOrCappedCommitHistoryDoesNotAuthorizeCleanup(int count)
    {
        var json = JsonSerializer.Serialize(Enumerable.Repeat(new { commit = new { committer = new { date = "2026-08-25T00:00:00Z" } } }, count));
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Response(json));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.GetPullRequestLatestActivityAsync("Azure", "azure-sdk-for-python", 1, CancellationToken.None));

        VerifyCalls(1);
    }

    [Test]
    public async Task CloseWithComment_StateOnlyPatchThenExactAuthenticatedPost()
    {
        const string body = "<!-- azsdk-abandoned-release-plan:123 -->\nPlan abandoned.";
        var methods = new List<HttpMethod>();
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, ct) =>
            {
                methods.Add(request.Method);
                Assert.That(request.Headers.Authorization?.Parameter, Is.EqualTo("existing-test-identity"));
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                if (request.Method == HttpMethod.Patch)
                {
                    Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo("https://api.github.com/repos/Azure/azure-sdk-for-python/pulls/1"));
                    Assert.That(json.RootElement.EnumerateObject().Select(p => p.Name), Is.EqualTo(new[] { "state" }));
                    Assert.That(json.RootElement.GetProperty("state").GetString(), Is.EqualTo("closed"));
                    return Response("{\"number\":1,\"state\":\"closed\"}");
                }
                Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo("https://api.github.com/repos/Azure/azure-sdk-for-python/issues/1/comments"));
                Assert.That(json.RootElement.EnumerateObject().Select(p => p.Name), Is.EqualTo(new[] { "body" }));
                Assert.That(json.RootElement.GetProperty("body").GetString(), Is.EqualTo(body));
                return Response("{\"id\":17,\"body\":\"saved\"}", HttpStatusCode.Created);
            });
        var pr = await _service.ClosePullRequestAsync("Azure", "azure-sdk-for-python", 1, CancellationToken.None, body);
        Assert.That(pr.State.Value, Is.EqualTo(ItemState.Closed));
        Assert.That(methods, Is.EqualTo(new[] { HttpMethod.Patch, HttpMethod.Post }));
        VerifyCalls(2);
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public void CommentError_AfterClose_PropagatesWithoutRetry(HttpStatusCode status)
    {
        var methods = new List<HttpMethod>();
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                methods.Add(request.Method);
                return Task.FromResult(request.Method == HttpMethod.Patch ? Response("{\"state\":\"closed\"}") : Response("{\"message\":\"offline failure\"}", status));
            });
        Assert.CatchAsync<ApiException>(async () =>
            await _service.ClosePullRequestAsync("Azure", "azure-sdk-for-python", 1, CancellationToken.None, "note"));
        Assert.That(methods, Is.EqualTo(new[] { HttpMethod.Patch, HttpMethod.Post }));
        VerifyCalls(2);
    }

    [Test]
    public void CloseWithComment_PreCanceled_DoesNotStartTransport()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () =>
            await _service.ClosePullRequestAsync("Azure", "azure-sdk-for-python", 1, cts.Token, "note"));
        VerifyCalls(0);
    }

    [Test]
    public async Task CancellationDuringComment_WaitsForTransportThenPropagates()
    {
        using var cts = new CancellationTokenSource();
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                if (request.Method == HttpMethod.Patch) { return Task.FromResult(Response("{\"state\":\"closed\"}")); }
                cts.Cancel();
                entered.SetResult();
                return pending.Task;
            });
        var operation = _service.ClosePullRequestAsync("Azure", "azure-sdk-for-python", 1, cts.Token, "note");
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(operation.IsCompleted, Is.False, "Octokit cannot cancel an accepted write.");
        }
        finally
        {
            pending.TrySetResult(Response("{\"id\":17}"));
        }
        var ex = Assert.CatchAsync<OperationCanceledException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.That(ex!.CancellationToken, Is.EqualTo(cts.Token));
        VerifyCalls(2);
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

    [TestCase(null)]
    [TestCase("")]
    public async Task Close_NoComment_OneAuthenticatedPatchWithOnlyState(string? comment)
    {
        const string repo = "azure-sdk-for-python";
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, ct) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(request.Method, Is.EqualTo(HttpMethod.Patch));
                    Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo($"https://api.github.com/repos/Azure/{repo}/pulls/17"));
                    Assert.That(request.Headers.Authorization?.Parameter, Is.EqualTo("existing-test-identity"));
                });
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                Assert.That(json.RootElement.EnumerateObject().Select(p => p.Name), Is.EqualTo(new[] { "state" }));
                Assert.That(json.RootElement.GetProperty("state").GetString(), Is.EqualTo("closed"));
                return Response("{\"number\":17,\"state\":\"closed\"}");
            });
        var pr = await _service.ClosePullRequestAsync("Azure", repo, 17, CancellationToken.None, comment);
        Assert.That(pr.State.Value, Is.EqualTo(ItemState.Closed));
        VerifyCalls(1);
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.NotFound)]
    [TestCase(HttpStatusCode.InternalServerError)]
    public void Close_ErrorDoesNotRetryPatch(HttpStatusCode status)
    {
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => Response("{\"message\":\"offline failure\"}", status));
        Assert.CatchAsync<ApiException>(async () =>
            await _service.ClosePullRequestAsync("Azure", "azure-sdk-for-python", 1, CancellationToken.None, "must not post"));
        VerifyCalls(1);
    }

    [Test]
    public void Close_PreCanceled_DoesNotStartTransport()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () =>
            await _service.ClosePullRequestAsync("Azure", "azure-sdk-for-python", 1, cts.Token));
        VerifyCalls(0);
    }

    [Test]
    public async Task Close_CancellationDuringPatch_WaitsThenSkipsComment()
    {
        using var cts = new CancellationTokenSource();
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback(() => { cts.Cancel(); entered.SetResult(); }).Returns(pending.Task);
        var operation = _service.ClosePullRequestAsync("Azure", "azure-sdk-for-python", 1, cts.Token, "must not post");
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(operation.IsCompleted, Is.False);
        }
        finally
        {
            pending.TrySetResult(Response("{\"state\":\"closed\"}"));
        }
        Assert.CatchAsync<OperationCanceledException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(10)));
        VerifyCalls(1);
    }

    [TestCase("Azure", "azure-sdk-for-python")]
    [TestCase("Azure", "azure-sdk-for-net")]
    [TestCase("Azure", "azure-sdk-for-java")]
    [TestCase("Azure", "azure-sdk-for-js")]
    [TestCase("Azure", "azure-sdk-for-go")]
    [TestCase("custom-owner", "custom.repo_1")]
    [TestCase("custom-owner", ".github")]
    public async Task UrlLookup_UsesFixedApiAndAnonymousRead(string owner, string repo)
    {
        _handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
                Assert.That(request.RequestUri!.AbsoluteUri, Is.EqualTo($"https://api.github.com/repos/{owner}/{repo}/pulls/17"));
                Assert.That(request.Headers.Authorization, Is.Null);
                return Task.FromResult(Response("{\"number\":17,\"state\":\"open\"}"));
            });
        var pr = await _service.GetPullRequestAsync($"https://github.com/{owner}/{repo}/pull/00017/", CancellationToken.None);
        Assert.That(pr.Number, Is.EqualTo(17));
        VerifyCalls(1);
    }

    [TestCase("http://github.com/Azure/repo/pull/1")]
    [TestCase("https://github.com.evil/Azure/repo/pull/1")]
    [TestCase("https://token@github.com/Azure/repo/pull/1")]
    [TestCase("https://github.com:443/Azure/repo/pull/1")]
    [TestCase("https://github.com/Azure/repo/pull/0")]
    [TestCase("https://github.com/Azure/repo/pull/-1")]
    [TestCase("https://github.com/Azure/repo/pull/2147483648")]
    [TestCase("https://github.com/Azure/repo/pull/1?x=1")]
    [TestCase("https://github.com/Azure/repo/pull/1#fragment")]
    [TestCase("https://github.com/Azure/repo/pull/1\n")]
    [TestCase("https://github.com/Azure/repo/issues/1")]
    [TestCase("https://github.com/Azure/../pull/1")]
    [TestCase("https://github.com/-bad/repo/pull/1")]
    [TestCase("https://github.com/bad--owner/repo/pull/1")]
    [TestCase("https://github.com/Azure/./pull/1")]
    [TestCase("https://github.com/Azure/re%70o/pull/1")]
    [TestCase("")]
    [TestCase(null)]
    public void UrlLookup_InvalidUrlRejectedBeforeTransport(string? url)
    {
        Assert.CatchAsync<ArgumentException>(async () => await _service.GetPullRequestAsync(url!, CancellationToken.None));
        VerifyCalls(0);
    }

    private void VerifyCalls(int count)
    {
        _handler.Protected().Verify("SendAsync", Times.Exactly(count), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        Assert.That(_process.Invocations, Is.Empty);
        Assert.That(_factory.Invocations, Is.Empty);
    }
}
