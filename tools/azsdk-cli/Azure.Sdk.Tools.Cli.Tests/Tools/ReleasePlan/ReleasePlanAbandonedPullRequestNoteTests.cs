// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan;

public class ReleasePlanAbandonedPullRequestNoteTests
{
    private Mock<IDevOpsService> _devOps = null!;
    private Mock<IGitHubService> _github = null!;
    private Mock<INotificationService> _notifications = null!;
    private ReleasePlanTool _tool = null!;
    private ReleasePlanWorkItem _plan = null!;
    private const string Repo = "azure-sdk-for-python";
    private readonly List<string> _operations = [];

    [SetUp]
    public void Setup()
    {
        _operations.Clear();
        _devOps = new(MockBehavior.Strict);
        _github = new(MockBehavior.Strict);
        _notifications = new(MockBehavior.Strict);
        _plan = new ReleasePlanWorkItem
        {
            WorkItemId = 42, ReleasePlanId = 123, Revision = 7, Status = "In Progress",
            SDKInfo = [new SDKInfo { Language = "Python", SdkPullRequestUrl = $"https://github.com/Azure/{Repo}/pull/1" }]
        };
        _devOps.Setup(s => s.GetReleasePlanAsync(123, true, It.IsAny<CancellationToken>())).ReturnsAsync(() => _plan);
        _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()))
            .Callback<int, Dictionary<string, string>, int, CancellationToken>((_, fields, _, _) =>
            {
                Assert.That(fields, Is.EquivalentTo(new Dictionary<string, string> { ["System.State"] = "Abandoned" }));
                _operations.Add("save");
            }).ReturnsAsync(new WorkItem());
        SetupPr(Repo, 1);
        _tool = new ReleasePlanTool(_devOps.Object, Mock.Of<IGitHelper>(), Mock.Of<ITypeSpecHelper>(),
            new TestLogger<ReleasePlanTool>(), Mock.Of<IUserHelper>(), _github.Object, Mock.Of<IEnvironmentHelper>(),
            new InputSanitizer(), new HttpClient(), Mock.Of<INpxHelper>(), Mock.Of<IRawOutputHelper>(), _notifications.Object);
    }

    private void SetupPr(string repo, int number)
    {
        _github.Setup(s => s.GetPullRequestAsync("Azure", repo, number, It.IsAny<CancellationToken>())).ReturnsAsync(Pr());
        _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", repo, number, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<IssueComment>());
        _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", repo, number, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => _operations.Add($"comment{number}")).ReturnsAsync(new IssueComment());
        _github.Setup(s => s.ClosePullRequestAsync("Azure", repo, number, It.IsAny<CancellationToken>()))
            .Callback(() => _operations.Add($"close{number}")).ReturnsAsync(Pr(state: "closed"));
    }

    private static PullRequest Pr(string? author = "azure-sdk-automation[bot]", string state = "open", bool merged = false, bool draft = false) =>
        new Octokit.Internal.SimpleJsonSerializer().Deserialize<PullRequest>(JsonSerializer.Serialize(new
        {
            state, merged, draft, user = new { login = author }, merged_at = merged ? "2026-10-08T00:00:00Z" : null
        }));

    private static IssueComment Comment(string body) =>
        new Octokit.Internal.SimpleJsonSerializer().Deserialize<IssueComment>(JsonSerializer.Serialize(new { body }));

    private void Comments(params string[] bodies) =>
        _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bodies.Select(Comment).ToArray());

    private void NoCleanup()
    {
        _github.Verify(s => s.GetPullRequestIssueCommentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _github.Verify(s => s.CreatePullRequestCommentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _github.Verify(s => s.ClosePullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void NoStateWrites() => _devOps.Verify(s => s.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

    [TearDown]
    public void NoUnrelatedSideEffects()
    {
        Assert.That(_notifications.Invocations, Is.Empty);
        Assert.That(_github.Invocations.Any(i => i.Method.Name is "UpdatePullRequestAsync" or "CreateIssueAsync"), Is.False);
        Assert.That(_devOps.Invocations.Any(i => i.Method.Name == "GetReleasePlanForWorkItemAsync"), Is.False);
    }

    [TestCase("human")]
    [TestCase("azure-sdk-automation[bot]")]
    [TestCase("other[bot]")]
    [TestCase(null)]
    public async Task SavedAbandonment_CommentsThenClosesRegardlessOfAuthor(string? author)
    {
        _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Pr(author));
        var response = await _tool.AbandonReleasePlan(123);
        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo("Success"));
            Assert.That(response.NextSteps, Is.Null);
            Assert.That(_operations, Is.EqualTo(new[] { "save", "comment1", "close1" }));
            Assert.That(response.Details[0], Does.Contain("plan 123"));
        });
        _devOps.Verify(s => s.GetReleasePlanAsync(123, true, It.IsAny<CancellationToken>()), Times.Once);
        _github.Verify(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", Repo, 1,
            $"<!-- azsdk-abandoned-release-plan:42 -->\nRelease plan [123]({_plan.ReleasePlanLink}) has been abandoned. Closing this linked SDK PR.", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("<!-- azsdk-abandoned-release-plan:42 -->", false)]
    [TestCase("Other\r\n<!-- azsdk-abandoned-release-plan:42 -->\r\nOld comment-only note", false)]
    [TestCase("<!-- azsdk-abandoned-release-plan:142 -->", true)]
    [TestCase("<!-- azsdk-abandoned-release-plan:43 -->", true)]
    [TestCase("Quoted <!-- azsdk-abandoned-release-plan:42 -->", true)]
    [TestCase("<!-- azsdk-abandoned-release-plan:42 --> suffix", true)]
    public async Task ExactMarker_DeduplicatesCommentButStillCloses(string body, bool posts)
    {
        Comments(body);
        var response = await _tool.AbandonReleasePlan(123);
        Assert.That(response.NextSteps, Is.Null);
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", Repo, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()), posts ? Times.Once : Times.Never);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("open", true)]
    [TestCase("closed", false)]
    [TestCase("closed", true)]
    public async Task AlreadyAbandonedClosedOrMerged_SkipsAllCleanup(string state, bool merged)
    {
        _plan.Status = "Abandoned";
        _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Pr(state: state, merged: merged));
        var response = await _tool.AbandonReleasePlan(123);
        Assert.That(response.NextSteps, Is.Null);
        NoCleanup();
        NoStateWrites();
        _github.Verify(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()), Times.Once);
        _devOps.Verify(s => s.GetReleasePlanAsync(123, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task AlreadyAbandonedDraft_CleansUpWithoutStateWrite()
    {
        _plan.Status = "Abandoned";
        _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Pr(draft: true));
        Assert.That((await _tool.AbandonReleasePlan(123)).NextSteps, Is.Null);
        Assert.That(_operations, Is.EqualTo(new[] { "comment1", "close1" }));
        NoStateWrites();
    }

    [TestCase("merged")]
    [TestCase("released")]
    [TestCase("finished")]
    [TestCase("malformed")]
    [TestCase("unknown")]
    public async Task GuardRejects_NoCleanupOrStateWrite(string failure)
    {
        if (failure == "merged") { _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Pr(merged: true)); }
        else if (failure == "released") { _plan.SDKInfo[0].ReleaseStatus = "Released"; }
        else if (failure == "finished") { _plan.Status = "Finished"; }
        else if (failure == "unknown") { _plan.Status = "Unknown"; }
        else { _plan.SDKInfo[0].SdkPullRequestUrl += "?untrusted=1"; }
        Assert.That((await _tool.AbandonReleasePlan(123)).ResponseError, Is.Not.Null);
        NoCleanup();
        NoStateWrites();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StateUpdateFails_NoCleanup(bool returnsNull)
    {
        var setup = _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()));
        if (returnsNull) { setup.ReturnsAsync((WorkItem)null!); }
        else { setup.ThrowsAsync(new InvalidOperationException("revision conflict")); }
        Assert.That((await _tool.AbandonReleasePlan(123)).ResponseError, Is.Not.Null);
        NoCleanup();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DuplicateCanonicalPublicOrPrivateLinks_OnlyOneCleanup(bool isPrivate)
    {
        var repo = isPrivate ? Repo + "-pr" : Repo;
        _plan.SDKInfo =
        [
            new SDKInfo { Language = "Python", SdkPullRequestUrl = $"https://github.com/Azure/{repo}/pull/1" },
            new SDKInfo { Language = "Python", SdkPullRequestUrl = $"https://github.com/azure/{repo.ToUpperInvariant()}/pull/0001/" }
        ];
        SetupPr(repo, 1);
        Assert.That((await _tool.AbandonReleasePlan(123)).NextSteps, Is.Null);
        _github.Verify(s => s.GetPullRequestAsync("Azure", repo, 1, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", repo, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", repo, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("pr", true)]
    [TestCase("null-pr", true)]
    [TestCase("unknown-pr", true)]
    [TestCase("comments", true)]
    [TestCase("null-comments", true)]
    [TestCase("post", true)]
    [TestCase("close", true)]
    [TestCase("comments", false)]
    [TestCase("post", false)]
    [TestCase("close", false)]
    public async Task OnePrFails_OtherContinues_NoRetriesAndPublicIdGuidance(string failure, bool alreadyAbandoned)
    {
        if (alreadyAbandoned) { _plan.Status = "Abandoned"; }
        _plan.SDKInfo.Add(new SDKInfo { Language = "Python", SdkPullRequestUrl = $"https://github.com/Azure/{Repo}/pull/2" });
        SetupPr(Repo, 2);
        if (failure == "pr") { _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("read failed")); }
        else if (failure == "null-pr") { _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ReturnsAsync((PullRequest)null!); }
        else if (failure == "unknown-pr") { _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Pr(state: "unexpected")); }
        else if (failure == "comments") { _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("comments failed")); }
        else if (failure == "null-comments") { _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ReturnsAsync((IReadOnlyList<IssueComment>)null!); }
        else if (failure == "post") { _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", Repo, 1, It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("comment failed")); }
        else { _github.Setup(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("close failed")); }
        var response = await _tool.AbandonReleasePlan(123);
        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo("Success"));
            Assert.That(response.ExitCode, Is.Zero);
            Assert.That(response.Details, Has.Some.Contains("Warning: plan 123 remains Abandoned"));
            Assert.That(response.NextSteps, Has.Count.EqualTo(1));
            Assert.That(response.NextSteps![0], Does.Contain("releasePlanId 123").And.Not.Contain("42"));
        });
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", Repo, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()), failure is "post" or "close" ? Times.Once : Times.Never);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()), failure == "close" ? Times.Once : Times.Never);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 2, It.IsAny<CancellationToken>()), Times.Once);
        if (alreadyAbandoned) { NoStateWrites(); }
        else { Assert.That(_operations[0], Is.EqualTo("save")); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CommentOrCloseFailure_RetryDeduplicatesSavedNoteWithoutAdoRewrite(bool closeFailure)
    {
        var comments = new List<IssueComment>();
        _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ReturnsAsync(() => comments);
        _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", Repo, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, body, _) => comments.Add(Comment(body)))
            .ReturnsAsync(new IssueComment());
        if (closeFailure)
        {
            _github.SetupSequence(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("close failed")).ReturnsAsync(Pr(state: "closed"));
        }
        else
        {
            _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", Repo, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, int, string, CancellationToken>((_, _, _, body, _) => comments.Add(Comment(body)))
                .ThrowsAsync(new IOException("POST saved but response lost"));
        }
        var first = await _tool.AbandonReleasePlan(123);
        Assert.That(first.Status, Is.EqualTo("Success"));
        Assert.That(first.NextSteps, Is.Not.Empty);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()), closeFailure ? Times.Once : Times.Never);
        _plan.Status = "Abandoned";
        var retry = await _tool.AbandonReleasePlan(123);
        Assert.That(retry.NextSteps, Is.Null);
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", Repo, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()), Times.Exactly(closeFailure ? 2 : 1));
        _devOps.Verify(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ClosedAfterFirstAttempt_RetryDoesNotRepeatCommentCloseOrAdoWrite(bool lostCloseResponse)
    {
        var closed = false;
        _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Pr(state: closed ? "closed" : "open"));
        _github.Setup(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                closed = true;
                return lostCloseResponse
                    ? Task.FromException<PullRequest>(new IOException("PATCH saved but response lost"))
                    : Task.FromResult(Pr(state: "closed"));
            });

        var first = await _tool.AbandonReleasePlan(123);
        Assert.That(first.Status, Is.EqualTo("Success"));
        Assert.That(first.NextSteps?.Count ?? 0, Is.EqualTo(lostCloseResponse ? 1 : 0));
        _plan.Status = "Abandoned";

        var retry = await _tool.AbandonReleasePlan(123);
        Assert.That(retry.Status, Is.EqualTo("Success"));
        Assert.That(retry.NextSteps, Is.Null);
        _github.Verify(s => s.GetPullRequestIssueCommentsAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()), Times.Once);
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", Repo, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()), Times.Once);
        _devOps.Verify(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()), Times.Once);
        _devOps.Verify(s => s.GetReleasePlanAsync(123, true, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestCase("null-sdk")]
    [TestCase("null-entry")]
    [TestCase("bad-url")]
    public async Task AlreadyAbandonedInvalidLinks_WarnsWithoutAnyWrite(string failure)
    {
        _plan.Status = "Abandoned";
        if (failure == "null-sdk") { _plan.SDKInfo = null!; }
        else if (failure == "null-entry") { _plan.SDKInfo.Add(null!); }
        else { _plan.SDKInfo[0].SdkPullRequestUrl += "?untrusted=1"; }
        var response = await _tool.AbandonReleasePlan(123);
        Assert.That(response.Status, Is.EqualTo("Success"));
        Assert.That(response.NextSteps, Is.Not.Empty);
        Assert.That(_github.Invocations, Is.Empty);
        NoStateWrites();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NoSdkLinks_NoGithubAccess(bool abandoned)
    {
        _plan.SDKInfo = [];
        if (abandoned) { _plan.Status = "Abandoned"; }
        Assert.That((await _tool.AbandonReleasePlan(123)).NextSteps, Is.Null);
        Assert.That(_github.Invocations, Is.Empty);
        if (abandoned) { NoStateWrites(); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DashboardMapping_PublicPlanIdWithPrimaryWorkItemMarker(bool isTest)
    {
        _plan.IsTestReleasePlan = isTest;
        Assert.That((await _tool.AbandonReleasePlan(123)).NextSteps, Is.Null);
        var expected = (isTest ? ReleasePlanWorkItem.DashboardBaseUrlTest : ReleasePlanWorkItem.DashboardBaseUrl) + "123";
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", Repo, 1,
            It.Is<string>(body => body.Contains(expected) && body.Contains("[123]") && body.StartsWith("<!-- azsdk-abandoned-release-plan:42 -->\n")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ActiveClosedUnmergedPr_AllowsSaveButNoPrMutation()
    {
        _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Pr(state: "closed"));
        Assert.That((await _tool.AbandonReleasePlan(123)).Status, Is.EqualTo("Success"));
        Assert.That(_operations, Is.EqualTo(new[] { "save" }));
        NoCleanup();
    }

    [TestCase("closed", false)]
    [TestCase("closed", true)]
    [TestCase("open", true)]
    public async Task PrChangesAfterSave_CleanupReadSkipsClosedOrMerged(string state, bool merged)
    {
        _github.SetupSequence(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pr()).ReturnsAsync(Pr(state: state, merged: merged));
        Assert.That((await _tool.AbandonReleasePlan(123)).Status, Is.EqualTo("Success"));
        NoCleanup();
        Assert.That(_operations, Is.EqualTo(new[] { "save" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ActiveNullSdkDetails_GuardRejectsWithoutAnyWrite(bool nullEntry)
    {
        if (nullEntry) { _plan.SDKInfo.Add(null!); }
        else { _plan.SDKInfo = null!; }
        Assert.That((await _tool.AbandonReleasePlan(123)).ResponseError, Is.Not.Null);
        Assert.That(_github.Invocations, Is.Empty);
        NoStateWrites();
    }

    [TestCase("before")]
    [TestCase("save")]
    [TestCase("read")]
    [TestCase("comments")]
    [TestCase("post")]
    [TestCase("close")]
    public void Cancellation_PropagatesAndNeverContinuesToAnotherPr(string phase)
    {
        using var cts = new CancellationTokenSource();
        _plan.SDKInfo.Add(new SDKInfo { Language = "Python", SdkPullRequestUrl = $"https://github.com/Azure/{Repo}/pull/2" });
        SetupPr(Repo, 2);
        if (phase == "before") { cts.Cancel(); }
        else if (phase == "save") { _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, cts.Token)).Callback(() => cts.Cancel()).ReturnsAsync(new WorkItem()); }
        else
        {
            _plan.Status = "Abandoned";
            if (phase == "read") { _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, cts.Token)).Callback(() => cts.Cancel()).ReturnsAsync(Pr()); }
            else if (phase == "comments") { _github.Setup(s => s.GetPullRequestIssueCommentsAsync("Azure", Repo, 1, cts.Token)).Callback(() => cts.Cancel()).ReturnsAsync(Array.Empty<IssueComment>()); }
            else if (phase == "post") { _github.Setup(s => s.CreatePullRequestCommentAsync("Azure", Repo, 1, It.IsAny<string>(), cts.Token)).Callback(() => cts.Cancel()).ReturnsAsync(new IssueComment()); }
            else { _github.Setup(s => s.ClosePullRequestAsync("Azure", Repo, 1, cts.Token)).Callback(() => cts.Cancel()).ReturnsAsync(Pr(state: "closed")); }
        }
        Assert.CatchAsync<OperationCanceledException>(async () => await _tool.AbandonReleasePlan(123, cts.Token));
        _github.Verify(s => s.GetPullRequestIssueCommentsAsync("Azure", Repo, 2, It.IsAny<CancellationToken>()), Times.Never);
        _github.Verify(s => s.CreatePullRequestCommentAsync("Azure", Repo, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()), phase is "post" or "close" ? Times.Once : Times.Never);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()), phase == "close" ? Times.Once : Times.Never);
        if (phase == "before") { Assert.That(_devOps.Invocations, Is.Empty); }
    }
}
