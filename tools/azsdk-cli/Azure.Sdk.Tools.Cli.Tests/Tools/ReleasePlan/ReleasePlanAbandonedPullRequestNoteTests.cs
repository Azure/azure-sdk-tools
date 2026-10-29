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
    private Mock<IRawOutputHelper> _output = null!;
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
        _output = new();
        _plan = new ReleasePlanWorkItem
        {
            WorkItemId = 42, ReleasePlanId = 123, Revision = 7, Status = "In Progress",
            SDKInfo = [new SDKInfo { Language = "Python", SdkPullRequestUrl = $"https://github.com/Azure/{Repo}/pull/1" }]
        };
        _devOps.Setup(s => s.GetReleasePlanAsync(123, It.IsAny<CancellationToken>(), true)).ReturnsAsync(() => _plan);
        _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()))
            .Callback<int, Dictionary<string, string>, int, CancellationToken>((_, fields, _, _) =>
            {
                Assert.That(fields, Is.EquivalentTo(new Dictionary<string, string> { ["System.State"] = "Abandoned" }));
                _operations.Add("save");
            }).ReturnsAsync(new WorkItem());
        SetupPr(Repo, 1);
        _tool = new ReleasePlanTool(_devOps.Object, Mock.Of<IGitHelper>(), Mock.Of<ITypeSpecHelper>(),
            new TestLogger<ReleasePlanTool>(), Mock.Of<IUserHelper>(), _github.Object, Mock.Of<IEnvironmentHelper>(),
            new InputSanitizer(), new HttpClient(), Mock.Of<INpxHelper>(), _output.Object, _notifications.Object);
    }

    private void SetupPr(string repo, int number)
    {
        _github.Setup(s => s.GetPullRequestAsync("Azure", repo, number, It.IsAny<CancellationToken>())).ReturnsAsync(Pr());
        _github.Setup(s => s.GetPullRequestAsync($"https://github.com/Azure/{repo}/pull/{number}", It.IsAny<CancellationToken>())).ReturnsAsync(Pr());
        _github.Setup(s => s.ClosePullRequestAsync("Azure", repo, number, It.IsAny<CancellationToken>(), It.IsAny<string>()))
            .Callback(() => { _operations.Add($"close{number}"); _operations.Add($"comment{number}"); }).ReturnsAsync(Pr(state: "closed"));
    }

    private static PullRequest Pr(string? author = "azure-sdk-automation[bot]", string state = "open", bool merged = false, bool draft = false) =>
        new Octokit.Internal.SimpleJsonSerializer().Deserialize<PullRequest>(JsonSerializer.Serialize(new
        {
            state, merged, draft, user = new { login = author }, merged_at = merged ? "2026-10-08T00:00:00Z" : null
        }));

    private void NoCleanup()
    {
        _github.Verify(s => s.GetPullRequestIssueCommentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _github.Verify(s => s.ClosePullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Never);
    }

    private void NoStateWrites() => _devOps.Verify(s => s.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

    [TearDown]
    public void NoUnrelatedSideEffects()
    {
        Assert.That(_notifications.Invocations, Is.Empty);
        Assert.That(_github.Invocations.Any(i => i.Method.Name is "UpdatePullRequestAsync" or "CreateIssueAsync"), Is.False);
        Assert.That(_devOps.Invocations.Any(i => i.Method.Name == "GetReleasePlanForWorkItemAsync"), Is.False);
        _github.Verify(s => s.GetPullRequestIssueCommentsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("human")]
    [TestCase("azure-sdk-automation[bot]")]
    [TestCase("other[bot]")]
    [TestCase(null)]
    public async Task SavedAbandonment_ClosesThenCommentsRegardlessOfAuthor(string? author)
    {
        _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Pr(author));
        _github.Setup(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", It.IsAny<CancellationToken>())).ReturnsAsync(Pr(author));
        var response = await _tool.AbandonReleasePlan(123);
        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo("Success"));
            Assert.That(response.NextSteps, Is.Null);
            Assert.That(_operations, Is.EqualTo(new[] { "save", "close1", "comment1" }));
            Assert.That(response.Details[0], Does.Contain("plan 123"));
        });
        _devOps.Verify(s => s.GetReleasePlanAsync(123, It.IsAny<CancellationToken>(), true), Times.Once);
        _github.Verify(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()), Times.Once);
        _github.Verify(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", It.IsAny<CancellationToken>()), Times.Once);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>(),
            $"<!-- azsdk-abandoned-release-plan:123 -->\nRelease plan [123]({_plan.ReleasePlanLink}) has been abandoned. Closing this linked SDK PR."), Times.Once);
    }

    [TestCase("open", true)]
    [TestCase("closed", false)]
    [TestCase("closed", true)]
    public async Task AlreadyAbandonedClosedOrMerged_SkipsAllCleanup(string state, bool merged)
    {
        _plan.Status = "Abandoned";
        _github.Setup(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", It.IsAny<CancellationToken>())).ReturnsAsync(Pr(state: state, merged: merged));
        var response = await _tool.AbandonReleasePlan(123);
        Assert.That(response.NextSteps, Is.Null);
        NoCleanup();
        NoStateWrites();
        _github.Verify(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", It.IsAny<CancellationToken>()), Times.Once);
        _devOps.Verify(s => s.GetReleasePlanAsync(123, It.IsAny<CancellationToken>(), true), Times.Once);
    }

    [Test]
    public async Task AlreadyAbandonedDraft_CleansUpWithoutStateWrite()
    {
        _plan.Status = "Abandoned";
        _github.Setup(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", It.IsAny<CancellationToken>())).ReturnsAsync(Pr(draft: true));
        Assert.That((await _tool.AbandonReleasePlan(123)).NextSteps, Is.Null);
        Assert.That(_operations, Is.EqualTo(new[] { "close1", "comment1" }));
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

    [Test]
    public async Task DuplicateCanonicalPublicLinks_OnlyOneCleanup()
    {
        _plan.SDKInfo =
        [
            new SDKInfo { Language = "Python", SdkPullRequestUrl = $"https://github.com/Azure/{Repo}/pull/1" },
            new SDKInfo { Language = " python ", SdkPullRequestUrl = $"https://github.com/azure/{Repo.ToUpperInvariant()}/pull/0001/" }
        ];
        Assert.That((await _tool.AbandonReleasePlan(123)).NextSteps, Is.Null);
        _github.Verify(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()), Times.Once);
        _github.Verify(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", It.IsAny<CancellationToken>()), Times.Once);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Once);
    }

    [TestCase("pr", true)]
    [TestCase("null-pr", true)]
    [TestCase("unknown-pr", true)]
    [TestCase("post", true)]
    [TestCase("close", true)]
    [TestCase("post", false)]
    [TestCase("close", false)]
    public async Task OnePrFails_OtherContinues_NoRetriesAndPublicIdGuidance(string failure, bool alreadyAbandoned)
    {
        if (alreadyAbandoned) { _plan.Status = "Abandoned"; }
        _plan.SDKInfo.Add(new SDKInfo { Language = "Python", SdkPullRequestUrl = $"https://github.com/Azure/{Repo}/pull/2" });
        SetupPr(Repo, 2);
        if (failure == "pr") { _github.Setup(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("read failed")); }
        else if (failure == "null-pr") { _github.Setup(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", It.IsAny<CancellationToken>())).ReturnsAsync((PullRequest)null!); }
        else if (failure == "unknown-pr") { _github.Setup(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", It.IsAny<CancellationToken>())).ReturnsAsync(Pr(state: "unexpected")); }
        else { _github.Setup(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>(), It.IsAny<string>())).ThrowsAsync(new IOException(failure == "post" ? "closed but comment failed" : "close failed")); }
        var response = await _tool.AbandonReleasePlan(123);
        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo("Success"));
            Assert.That(response.ExitCode, Is.Zero);
            Assert.That(response.Details, Has.Some.Contains("Warning: plan 123 remains Abandoned"));
            Assert.That(response.NextSteps, Has.Count.EqualTo(1));
            Assert.That(response.NextSteps![0], Is.EqualTo("Review all SDK pull requests linked to release plan 123; close any remaining open unmerged PRs and add the abandonment notice where missing."));
        });
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>(), It.IsAny<string>()), failure is "post" or "close" ? Times.Once : Times.Never);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 2, It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Once);
        if (alreadyAbandoned) { NoStateWrites(); }
        else { Assert.That(_operations[0], Is.EqualTo("save")); }
    }

    [TestCase("success")]
    [TestCase("close-response-lost")]
    [TestCase("comment-failed")]
    [TestCase("comment-response-lost")]
    public async Task ClosedAfterFirstAttempt_RetryDoesNotRepairNoticeOrRewriteAdo(string outcome)
    {
        var closed = false;
        _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Pr(state: closed ? "closed" : "open"));
        _github.Setup(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Pr(state: closed ? "closed" : "open"));
        _github.Setup(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>(), It.IsAny<string>()))
            .Returns(() =>
            {
                closed = true;
                return outcome != "success"
                    ? Task.FromException<PullRequest>(new IOException(outcome))
                    : Task.FromResult(Pr(state: "closed"));
            });

        var first = await _tool.AbandonReleasePlan(123);
        Assert.That(first.Status, Is.EqualTo("Success"));
        Assert.That(first.NextSteps?.Count ?? 0, Is.EqualTo(outcome != "success" ? 1 : 0));
        _plan.Status = "Abandoned";

        var retry = await _tool.AbandonReleasePlan(123);
        Assert.That(retry.Status, Is.EqualTo("Success"));
        Assert.That(retry.NextSteps, Is.Null);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Once);
        _devOps.Verify(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()), Times.Once);
        _devOps.Verify(s => s.GetReleasePlanAsync(123, It.IsAny<CancellationToken>(), true), Times.Exactly(2));
    }

    [TestCase("null-sdk")]
    [TestCase("null-entry")]
    [TestCase("bad-url")]
    [TestCase("private")]
    [TestCase("wrong-language")]
    public async Task AlreadyAbandonedInvalidLinks_WarnsWithoutAnyWrite(string failure)
    {
        _plan.Status = "Abandoned";
        if (failure == "null-sdk") { _plan.SDKInfo = null!; }
        else if (failure == "null-entry") { _plan.SDKInfo.Add(null!); }
        else if (failure == "private") { _plan.SDKInfo[0].SdkPullRequestUrl = $"https://github.com/Azure/{Repo}-pr/pull/1"; }
        else if (failure == "wrong-language") { _plan.SDKInfo[0].Language = "Java"; }
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
    public async Task DashboardMapping_PublicPlanIdInLinkAndMarker(bool isTest)
    {
        _plan.IsTestReleasePlan = isTest;
        Assert.That((await _tool.AbandonReleasePlan(123)).NextSteps, Is.Null);
        var expected = (isTest ? ReleasePlanWorkItem.DashboardBaseUrlTest : ReleasePlanWorkItem.DashboardBaseUrl) + "123";
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>(),
            It.Is<string>(body => body.Contains(expected) && body.Contains("[123]") && body.StartsWith("<!-- azsdk-abandoned-release-plan:123 -->\n"))), Times.Once);
    }

    [Test]
    public async Task ActiveClosedUnmergedPr_AllowsSaveButNoPrMutation()
    {
        _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Pr(state: "closed"));
        _github.Setup(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", It.IsAny<CancellationToken>())).ReturnsAsync(Pr(state: "closed"));
        Assert.That((await _tool.AbandonReleasePlan(123)).Status, Is.EqualTo("Success"));
        Assert.That(_operations, Is.EqualTo(new[] { "save" }));
        NoCleanup();
    }

    [TestCase("closed", false)]
    [TestCase("closed", true)]
    [TestCase("open", true)]
    public async Task PrChangesAfterSave_CleanupReadSkipsClosedOrMerged(string state, bool merged)
    {
        _github.Setup(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pr(state: state, merged: merged));
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
    [TestCase("guard")]
    [TestCase("save")]
    [TestCase("save-uncertain")]
    [TestCase("save-transport-error")]
    [TestCase("read")]
    [TestCase("close")]
    public void Cancellation_PropagatesAndNeverContinuesToAnotherPr(string phase)
    {
        using var cts = new CancellationTokenSource();
        _plan.SDKInfo.Add(new SDKInfo { Language = "Python", SdkPullRequestUrl = $"https://github.com/Azure/{Repo}/pull/2" });
        SetupPr(Repo, 2);
        if (phase == "before") { cts.Cancel(); }
        else if (phase == "guard") { _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 1, cts.Token)).Callback(() => cts.Cancel()).ReturnsAsync(Pr()); }
        else if (phase == "save") { _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, cts.Token)).Callback(() => cts.Cancel()).ReturnsAsync(new WorkItem()); }
        else if (phase == "save-uncertain") { _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, cts.Token)).Callback(() => cts.Cancel()).ThrowsAsync(new OperationCanceledException(cts.Token)); }
        else if (phase == "save-transport-error") { _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, cts.Token)).Callback(() => cts.Cancel()).ThrowsAsync(new IOException("save response lost while canceled")); }
        else
        {
            _plan.Status = "Abandoned";
            if (phase == "read") { _github.Setup(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", cts.Token)).Callback(() => cts.Cancel()).ReturnsAsync(Pr()); }
            else { _github.Setup(s => s.ClosePullRequestAsync("Azure", Repo, 1, cts.Token, It.IsAny<string>())).Callback(() => cts.Cancel()).ReturnsAsync(Pr(state: "closed")); }
        }
        var ex = Assert.CatchAsync<OperationCanceledException>(async () => await _tool.AbandonReleasePlan(123, cts.Token));
        Assert.That(ex!.CancellationToken, Is.EqualTo(cts.Token));
        if (phase is not ("before" or "guard"))
        {
            Assert.That(ex.Message, Does.Contain("123").And.Contain("PR"));
            Assert.That(ex.InnerException, phase == "save-transport-error" ? Is.InstanceOf<IOException>() : Is.InstanceOf<OperationCanceledException>());
            Assert.That(ex.Message, phase is "save-uncertain" or "save-transport-error" ? Does.Contain("may already have committed") : Does.Contain("remains Abandoned"));
        }
        _output.Verify(o => o.OutputConsoleWarning(It.Is<string>(message => message.Contains("123") && (message.Contains("inspect", StringComparison.OrdinalIgnoreCase) || message.Contains("Review")))), phase is "before" or "guard" ? Times.Never : Times.Once);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 2, It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Never);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>(), It.IsAny<string>()), phase == "close" ? Times.Once : Times.Never);
        if (phase == "before") { Assert.That(_devOps.Invocations, Is.Empty); }
        if (phase is "before" or "guard") { NoStateWrites(); }
    }

    [Test]
    public async Task FailedClose_OpenRetryCompletesWithoutAnotherAdoWrite()
    {
        _github.SetupSequence(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>(), It.IsAny<string>()))
            .ThrowsAsync(new IOException("close was rejected")).ReturnsAsync(Pr(state: "closed"));
        Assert.That((await _tool.AbandonReleasePlan(123)).NextSteps, Is.Not.Empty);
        _plan.Status = "Abandoned";
        Assert.That((await _tool.AbandonReleasePlan(123)).NextSteps, Is.Null);
        _devOps.Verify(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()), Times.Once);
        _github.Verify(s => s.ClosePullRequestAsync("Azure", Repo, 1, It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Exactly(2));
    }

    [TestCase("read")]
    [TestCase("close-or-comment")]
    public async Task CancellationStopsWaiting_WarnsAndNeverStartsNextPr(string phase)
    {
        using var cts = new CancellationTokenSource();
        _plan.Status = "Abandoned";
        _plan.SDKInfo.Add(new SDKInfo { Language = "Python", SdkPullRequestUrl = $"https://github.com/Azure/{Repo}/pull/2" });
        SetupPr(Repo, 2);
        var pending = new TaskCompletionSource<PullRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (phase == "read")
        {
            _github.Setup(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/1", cts.Token))
                .Callback(() => cts.Cancel()).Returns(pending.Task);
        }
        else
        {
            _github.Setup(s => s.ClosePullRequestAsync("Azure", Repo, 1, cts.Token, It.IsAny<string>()))
                .Callback(() => cts.Cancel()).Returns(pending.Task);
        }
        try
        {
            var operation = _tool.AbandonReleasePlan(123, cts.Token);
            var ex = Assert.CatchAsync<OperationCanceledException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.That(ex!.CancellationToken, Is.EqualTo(cts.Token));
            Assert.That(ex.Message, Does.Contain("123").And.Contain("may already be closed").And.Contain("manually"));
            _output.Verify(o => o.OutputConsoleWarning(ex.Message), Times.Once);
            _github.Verify(s => s.GetPullRequestAsync($"https://github.com/Azure/{Repo}/pull/2", It.IsAny<CancellationToken>()), Times.Never);
            NoStateWrites();
        }
        finally
        {
            pending.TrySetResult(Pr(state: "closed"));
            await pending.Task;
        }
    }
}
