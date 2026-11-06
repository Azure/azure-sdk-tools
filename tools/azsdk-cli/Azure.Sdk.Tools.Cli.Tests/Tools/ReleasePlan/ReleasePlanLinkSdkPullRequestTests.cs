// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Moq;
using Octokit;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan;

[TestFixture]
public class ReleasePlanLinkSdkPullRequestTests
{
    private const string Repo = "azure-sdk-for-python";
    private const string PrUrl = "https://github.com/Azure/azure-sdk-for-python/pull/42";
    private Mock<IDevOpsService> _devOps = null!;
    private Mock<IGitHubService> _github = null!;
    private ReleasePlanWorkItem _plan = null!;
    private ReleasePlanTool _tool = null!;
    private HttpClient _httpClient = null!;
    private List<string> _labels = null!;

    [SetUp]
    public void SetUp()
    {
        _plan = new ReleasePlanWorkItem
        {
            WorkItemId = 100,
            ReleasePlanId = 10,
            Status = "In Progress",
            ApiReleaseType = ApiReleaseType.GA,
            IsManagementPlane = true,
            ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/123",
            SpecAPIVersion = "2026-01-01"
        };
        _labels = ["existing-service-label"];
        _devOps = new Mock<IDevOpsService>(MockBehavior.Strict);
        _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(_plan);
        _devOps.Setup(s => s.GetReleasePlanAsync(10, It.IsAny<CancellationToken>(), false)).ReturnsAsync(_plan);
        _devOps.Setup(s => s.AddSdkInfoInReleasePlanAsync(100, "Python", "", PrUrl, "Completed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _github = new Mock<IGitHubService>(MockBehavior.Strict);
        _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Pr(labels: _labels));
        _github.Setup(s => s.UpdatePullRequestAsync("Azure", Repo, 42, "SDK changes", It.IsAny<string>(), ItemState.Open, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _github.Setup(s => s.AddPullRequestLabelAsync("Azure", Repo, 42, "auto-release", It.IsAny<CancellationToken>()))
            .Callback(() => _labels.Add("auto-release"))
            .Returns(Task.CompletedTask);
        _httpClient = new HttpClient();
        _tool = new ReleasePlanTool(_devOps.Object, Mock.Of<IGitHelper>(), Mock.Of<ITypeSpecHelper>(),
            new TestLogger<ReleasePlanTool>(), Mock.Of<IUserHelper>(), _github.Object,
            Mock.Of<IEnvironmentHelper>(), new InputSanitizer(), _httpClient, Mock.Of<INpxHelper>(),
            Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());
    }

    [TearDown]
    public void TearDown() => _httpClient.Dispose();

    [TestCase(false)]
    [TestCase(true)]
    public async Task Link_EligibleManuallyGeneratedPr_AddsLabelAfterLinking(bool useReleasePlanId)
    {
        _github.Setup(s => s.AddPullRequestLabelAsync("Azure", Repo, 42, "auto-release", It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                VerifyLink();
                _labels.Add("auto-release");
            })
            .Returns(Task.CompletedTask);

        var response = await _tool.LinkSdkPullRequestToReleasePlan("Python", PrUrl,
            workItemId: useReleasePlanId ? 0 : 100, releasePlanId: useReleasePlanId ? 10 : 0);

        Assert.Multiple(() =>
        {
            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.PackageType, Is.EqualTo(SdkType.Management));
            Assert.That(response.Details, Has.Some.Contains("Added auto-release"));
            Assert.That(_labels, Is.EquivalentTo(new[] { "existing-service-label", "auto-release" }));
        });
        VerifyLink();
        VerifyLabel(Times.Once());
    }

    [TestCase("draft")]
    [TestCase("closed")]
    [TestCase("merged")]
    [TestCase("other-target")]
    [TestCase("private-repo")]
    [TestCase("missing-repo")]
    [TestCase("test-plan")]
    [TestCase("private-preview")]
    [TestCase("abandoned")]
    [TestCase("finished")]
    [TestCase("data-plane")]
    [TestCase("mixed-plane")]
    [TestCase("unspecified-plane")]
    public async Task Link_IneligiblePrOrPlan_StillLinksWithoutLabeling(string reason)
    {
        switch (reason)
        {
            case "test-plan": _plan.IsTestReleasePlan = true; break;
            case "private-preview": _plan.ApiReleaseType = ApiReleaseType.PrivatePreview; break;
            case "abandoned": _plan.Status = "Abandoned"; break;
            case "finished": _plan.Status = "Finished"; break;
            case "data-plane": _plan.IsManagementPlane = false; _plan.IsDataPlane = true; break;
            case "mixed-plane": _plan.IsDataPlane = true; break;
            case "unspecified-plane": _plan.IsManagementPlane = false; break;
        }
        var state = reason is "closed" or "merged" ? "closed" : "open";
        _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pr(state: state, draft: reason == "draft", merged: reason == "merged",
                target: reason == "other-target" ? "release/1.0" : "main",
                isPrivate: reason == "private-repo", missingRepo: reason == "missing-repo"));
        _github.Setup(s => s.UpdatePullRequestAsync("Azure", Repo, 42, "SDK changes", It.IsAny<string>(),
            state == "closed" ? ItemState.Closed : ItemState.Open, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var response = await _tool.LinkSdkPullRequestToReleasePlan("Python", PrUrl, workItemId: 100);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Details, Has.Some.Contains("Skipped auto-release"));
        VerifyLabel(Times.Never());
        _devOps.Verify(s => s.AddSdkInfoInReleasePlanAsync(100, "Python", "", PrUrl, "Completed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Link_ExistingLabelAndDescription_DoesNotWriteEitherAgain()
    {
        _labels.Add("AUTO-RELEASE");
        _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pr(body: "SDK changes\n## Release Plan Details\nAlready linked", labels: _labels));

        var response = await _tool.LinkSdkPullRequestToReleasePlan("Python", PrUrl, workItemId: 100);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Details, Has.Some.Contains("already has the auto-release"));
        VerifyLabel(Times.Never());
        _github.Verify(s => s.UpdatePullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ItemState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Link_ExistingDescriptionWithoutLabel_StillAddsLabel()
    {
        _github.Setup(s => s.GetPullRequestAsync("Azure", Repo, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pr(body: "SDK changes\n## Release Plan Details\nAlready linked"));

        var response = await _tool.LinkSdkPullRequestToReleasePlan("Python", PrUrl, workItemId: 100);

        Assert.That(response.ResponseError, Is.Null);
        VerifyLabel(Times.Once());
        _github.Verify(s => s.UpdatePullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ItemState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Link_PrBecomesIneligibleDuringLinking_DoesNotLabel(bool draft)
    {
        _github.SetupSequence(s => s.GetPullRequestAsync("Azure", Repo, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pr())
            .ReturnsAsync(Pr(draft: draft, state: draft ? "open" : "closed"));

        var response = await _tool.LinkSdkPullRequestToReleasePlan("Python", PrUrl, workItemId: 100);

        Assert.That(response.ResponseError, Is.Null);
        Assert.That(response.Details, Has.Some.Contains("Skipped auto-release"));
        VerifyLabel(Times.Never());
    }

    [Test]
    public async Task Link_EligibilityRecheckFails_ReportsPartialCompletionWithoutLabeling()
    {
        _github.SetupSequence(s => s.GetPullRequestAsync("Azure", Repo, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Pr())
            .ThrowsAsync(new InvalidOperationException("GitHub unavailable"));

        var response = await _tool.LinkSdkPullRequestToReleasePlan("Python", PrUrl, workItemId: 100);

        Assert.That(response.ResponseError, Does.Contain("was linked").And.Contain("GitHub unavailable"));
        Assert.That(response.ExitCode, Is.EqualTo(1));
        VerifyLink();
        VerifyLabel(Times.Never());
    }

    [Test]
    public async Task Link_RepeatedLinking_AddsLabelOnlyOnce()
    {
        var first = await _tool.LinkSdkPullRequestToReleasePlan("Python", PrUrl, workItemId: 100);
        var second = await _tool.LinkSdkPullRequestToReleasePlan("Python", PrUrl, workItemId: 100);

        Assert.That(first.ResponseError, Is.Null);
        Assert.That(second.ResponseError, Is.Null);
        Assert.That(_labels, Is.EquivalentTo(new[] { "existing-service-label", "auto-release" }));
        VerifyLabel(Times.Once());
    }

    [Test]
    public async Task Link_LabelFailure_ReportsPartialCompletionAndCanRetry()
    {
        _github.SetupSequence(s => s.AddPullRequestLabelAsync("Azure", Repo, 42, "auto-release", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("GitHub denied label permission"))
            .Returns(Task.CompletedTask);

        var failed = await _tool.LinkSdkPullRequestToReleasePlan("Python", PrUrl, workItemId: 100);
        var retry = await _tool.LinkSdkPullRequestToReleasePlan("Python", PrUrl, workItemId: 100);

        Assert.Multiple(() =>
        {
            Assert.That(failed.ResponseError, Does.Contain("was linked").And.Contain("failed to add auto-release")
                .And.Contain("GitHub denied label permission"));
            Assert.That(failed.Status, Is.EqualTo("Failed"));
            Assert.That(failed.ExitCode, Is.EqualTo(1));
            Assert.That(failed.OperationStatus, Is.EqualTo(Azure.Sdk.Tools.Cli.Models.Status.Failed));
            Assert.That(failed.Language, Is.EqualTo(SdkLanguage.Python));
            Assert.That(retry.ResponseError, Is.Null);
        });
        VerifyLabel(Times.Exactly(2));
    }

    [Test]
    public async Task Link_ReleasePlanWriteReturnsFalse_DoesNotLabel()
    {
        _devOps.Setup(s => s.AddSdkInfoInReleasePlanAsync(100, "Python", "", PrUrl, "Completed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await _tool.LinkSdkPullRequestToReleasePlan("Python", PrUrl, workItemId: 100);

        Assert.That(response.ResponseError, Does.Contain("Failed to save"));
        VerifyLabel(Times.Never());
    }

    [Test]
    public async Task Link_ReleasePlanWriteThrows_DoesNotLabel()
    {
        _devOps.Setup(s => s.AddSdkInfoInReleasePlanAsync(100, "Python", "", PrUrl, "Completed", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ADO unavailable"));

        var response = await _tool.LinkSdkPullRequestToReleasePlan("Python", PrUrl, workItemId: 100);

        Assert.That(response.ResponseError, Does.Contain("ADO unavailable"));
        VerifyLabel(Times.Never());
    }

    [Test]
    public void Link_LabelingCancelled_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        _github.Setup(s => s.AddPullRequestLabelAsync("Azure", Repo, 42, "auto-release", cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _tool.LinkSdkPullRequestToReleasePlan("Python", PrUrl, workItemId: 100, ct: cts.Token));
    }

    private void VerifyLink()
    {
        _devOps.Verify(s => s.AddSdkInfoInReleasePlanAsync(100, "Python", "", PrUrl, "Completed", It.IsAny<CancellationToken>()), Times.Once);
        _github.Verify(s => s.UpdatePullRequestAsync("Azure", Repo, 42, "SDK changes",
            It.Is<string>(body => body.StartsWith("Manually generated SDK changes")
                && body.Contains("## Release Plan Details")
                && body.Contains(_plan.ReleasePlanLink)
                && body.Contains(_plan.ActiveSpecPullRequest)
                && body.Contains(_plan.SpecAPIVersion)),
            ItemState.Open, It.IsAny<CancellationToken>()), Times.Once);
    }

    private void VerifyLabel(Times times) =>
        _github.Verify(s => s.AddPullRequestLabelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), times);

    private static PullRequest Pr(string state = "open", bool draft = false, bool merged = false,
        string target = "main", bool isPrivate = false, bool missingRepo = false,
        string body = "Manually generated SDK changes", List<string>? labels = null) =>
        new Octokit.Internal.SimpleJsonSerializer().Deserialize<PullRequest>(JsonSerializer.Serialize(new
        {
            state, draft, merged, title = "SDK changes", body,
            @base = new { @ref = target, repo = missingRepo ? null : new { @private = isPrivate } },
            labels = (labels ?? []).Select(name => new { name })
        }));
}
