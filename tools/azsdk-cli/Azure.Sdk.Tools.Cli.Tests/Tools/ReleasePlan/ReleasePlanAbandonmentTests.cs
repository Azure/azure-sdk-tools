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

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan
{
    internal class ReleasePlanAbandonmentTests
    {
        private Mock<IDevOpsService> _devOps = null!;
        private Mock<IGitHubService> _github = null!;
        private Mock<INotificationService> _notifications = null!;
        private ReleasePlanTool _tool = null!;
        private ReleasePlanWorkItem _plan = null!;
        private ReleasePlanWorkItem _fresh = null!;

        [SetUp]
        public void Setup()
        {
            _devOps = new Mock<IDevOpsService>(MockBehavior.Strict);
            _github = new Mock<IGitHubService>(MockBehavior.Strict);
            _notifications = new Mock<INotificationService>(MockBehavior.Strict);
            _plan = Plan();
            _fresh = Plan();
            _devOps.SetupSequence(s => s.GetReleasePlanForWorkItemAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _plan).ReturnsAsync(() => _fresh);
            _devOps.Setup(s => s.GetReleasePlanAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync(() => _plan);
            _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorkItem { Id = 42, Rev = 8, Fields = new Dictionary<string, object> { ["System.State"] = "Abandoned" } });
            _tool = new ReleasePlanTool(_devOps.Object, Mock.Of<IGitHelper>(), Mock.Of<ITypeSpecHelper>(),
                new TestLogger<ReleasePlanTool>(), Mock.Of<IUserHelper>(), _github.Object,
                Mock.Of<IEnvironmentHelper>(), new InputSanitizer(), new HttpClient(), Mock.Of<INpxHelper>(),
                Mock.Of<IRawOutputHelper>(), _notifications.Object);
        }

        private static ReleasePlanWorkItem Plan() => new()
        {
            WorkItemId = 42, ReleasePlanId = 123, Revision = 7, Status = "In Progress",
            SDKInfo = []
        };

        private static SDKInfo Sdk(string language, string repo, int number) => new()
        {
            Language = language, SdkPullRequestUrl = $"https://github.com/Azure/{repo}/pull/{number}",
            PullRequestStatus = "Closed", ReleaseStatus = "Not started"
        };

        private void SetSdks(params SDKInfo[] sdks)
        {
            _plan.SDKInfo = sdks.ToList();
            _fresh.SDKInfo = sdks.Select(s => new SDKInfo
            {
                Language = s.Language, SdkPullRequestUrl = s.SdkPullRequestUrl,
                ReleaseStatus = s.ReleaseStatus, PullRequestStatus = s.PullRequestStatus
            }).ToList();
        }

        private static PullRequest Pr(string state = "open", bool merged = false, bool draft = false)
        {
            var json = JsonSerializer.Serialize(new
            {
                state, merged, draft, merged_at = merged ? "2026-10-08T00:00:00Z" : null
            });
            return new Octokit.Internal.SimpleJsonSerializer().Deserialize<PullRequest>(json);
        }

        private void Lookup(string repo, int number, PullRequest? pr)
        {
            _github.Setup(g => g.GetPullRequestAsync("Azure", repo, number, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pr!);
        }

        private void NoWrites()
        {
            Assert.That(_devOps.Invocations.Any(i => i.Method.Name.StartsWith("Update", StringComparison.Ordinal)), Is.False);
            Assert.That(_notifications.Invocations, Is.Empty);
            Assert.That(_plan.Status, Is.EqualTo("In Progress"));
            Assert.That(_fresh.Status, Is.EqualTo("In Progress"));
        }

        [TestCase(0, 0)]
        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(42, -1)]
        public async Task InvalidIds_NoReadsOrWrites(int workItemId, int releasePlanId)
        {
            var response = await _tool.AbandonReleasePlan(workItemId, releasePlanId);
            Assert.That(response.ResponseError, Is.Not.Null);
            Assert.That(_devOps.Invocations, Is.Empty);
            NoWrites();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task NoSdkLinks_AllowsLegacyPlans_WithGuardedStateOnlyWrite(bool byReleasePlanId)
        {
            var response = await _tool.AbandonReleasePlan(byReleasePlanId ? 0 : 42, byReleasePlanId ? 123 : 0);
            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.Status, Is.EqualTo("Success"));
            _devOps.Verify(s => s.UpdateWorkItemAsync(42,
                It.Is<Dictionary<string, string>>(f => f.Count == 1 && f["System.State"] == "Abandoned"),
                7, It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(_github.Invocations, Is.Empty);
            Assert.That(_notifications.Invocations, Is.Empty);
            Assert.That(_plan.Status, Is.EqualTo("In Progress"));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task AnyLiveMergedSdk_BlocksDespiteStaleCachedStatus(int mergedIndex)
        {
            SetSdks(Sdk(".NET", "azure-sdk-for-net", 1), Sdk("Python", "azure-sdk-for-python", 2),
                Sdk("JavaScript", "azure-sdk-for-js", 3));
            Lookup("azure-sdk-for-net", 1, Pr(merged: mergedIndex == 0));
            Lookup("azure-sdk-for-python", 2, Pr(merged: mergedIndex == 1));
            Lookup("azure-sdk-for-js", 3, Pr(merged: mergedIndex == 2));
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Does.Contain("exception").And.Contain("unreleased").And.Contain("SDK Release Support"));
            NoWrites();
        }

        [TestCase("open", false)]
        [TestCase("open", true)]
        [TestCase("closed", false)]
        public async Task AllUnmergedMixedLanguages_AllowsOpenDraftAndClosed(string state, bool draft)
        {
            SetSdks(Sdk("c#", "azure-sdk-for-net", 1), Sdk("typescript", "azure-sdk-for-js", 2),
                Sdk("Python", "azure-sdk-for-python", 3), Sdk("Go", "azure-sdk-for-go", 4), Sdk("Java", "azure-sdk-for-java", 5));
            foreach (var sdk in _plan.SDKInfo)
            {
                var uri = new Uri(sdk.SdkPullRequestUrl);
                Lookup(uri.Segments[2].Trim('/'), int.Parse(uri.Segments[4]), Pr(state, draft: draft));
                sdk.PullRequestStatus = "Merged";
            }
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Is.Null);
            Assert.That(_github.Invocations.Count, Is.EqualTo(5));
            _github.Verify(g => g.IsPullRequestApprovedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task PrivateSdkRepository_UsesActualRepositoryForLiveRead()
        {
            SetSdks(Sdk("Python", "azure-sdk-for-python-pr", 1));
            Lookup("azure-sdk-for-python-pr", 1, Pr(merged: true));
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Is.Not.Null);
            _github.VerifyAll();
            NoWrites();
        }

        [Test]
        public async Task MixedOpenDraftClosed_DeduplicatesCanonicalLinksAndIgnoresCachedStatuses()
        {
            SetSdks(Sdk(".NET", "azure-sdk-for-net", 1), Sdk("Python", "azure-sdk-for-python", 2),
                Sdk("JavaScript", "azure-sdk-for-js", 3), Sdk("Python", "azure-sdk-for-python", 2));
            _plan.SDKInfo[3].SdkPullRequestUrl = "https://github.com/azure/AZURE-SDK-FOR-PYTHON/pull/0002/";
            _fresh.SDKInfo.Reverse();
            Lookup("azure-sdk-for-net", 1, Pr());
            Lookup("azure-sdk-for-python", 2, Pr(draft: true));
            Lookup("azure-sdk-for-js", 3, Pr("closed"));
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Is.Null);
            Assert.That(_github.Invocations.Count, Is.EqualTo(3));
        }

        [TestCase("New")]
        [TestCase("Not Started")]
        [TestCase("In Progress")]
        public async Task KnownActiveStateWithoutSdkLinks_AllowsAbandonment(string status)
        {
            _plan.Status = status;
            _fresh.Status = status;
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Is.Null);
        }

        [TestCase("null-plan")]
        [TestCase("workitem")]
        [TestCase("zero-workitem")]
        [TestCase("sdk-list")]
        [TestCase("sdk-entry")]
        public async Task MissingOrMismatchedPlanData_NoWrites(string invalid)
        {
            switch (invalid)
            {
                case "null-plan": _plan = null!; break;
                case "workitem": _plan.WorkItemId = 100; break;
                case "zero-workitem": _plan.WorkItemId = 0; break;
                case "sdk-list": _plan.SDKInfo = null!; break;
                case "sdk-entry": _plan.SDKInfo.Add(null!); break;
            }
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Is.Not.Null);
            Assert.That(_devOps.Invocations.Any(i => i.Method.Name.StartsWith("Update", StringComparison.Ordinal)), Is.False);
            Assert.That(_github.Invocations, Is.Empty);
        }

        [Test]
        public async Task ChangeDuringLiveRead_UsesOriginalRevisionAndNeverRetries()
        {
            SetSdks(Sdk("Python", "azure-sdk-for-python", 1));
            _github.Setup(g => g.GetPullRequestAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
                .Callback(() => _fresh.Revision++).ReturnsAsync(Pr());
            _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("ADO /rev conflict"));
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Does.Contain("/rev conflict"));
            _devOps.Verify(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(_notifications.Invocations, Is.Empty);
        }

        [TestCase("Released")]
        [TestCase("released")]
        [TestCase("RELEASED")]
        public async Task AnyRecordedRelease_BlocksWithoutGithubRead(string status)
        {
            SetSdks(new SDKInfo { Language = "Python", ReleaseStatus = status }, Sdk("Java", "azure-sdk-for-java", 1));
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Does.Contain("SDK Release Support"));
            Assert.That(_github.Invocations, Is.Empty);
            NoWrites();
        }

        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/0")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/2147483648")]
        [TestCase("https://github.com/Azure/azure-sdk-for-java/pull/1")]
        [TestCase("https://github.com/Azure/azure-sdk-for-fake/pull/1")]
        [TestCase("http://github.com/Azure/azure-sdk-for-python/pull/1")]
        [TestCase("https://github.com.evil/Azure/azure-sdk-for-python/pull/1")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/1/files")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/1?query=true")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/1#fragment")]
        [TestCase("https://user@github.com/Azure/azure-sdk-for-python/pull/1")]
        [TestCase("https://github.com:444/Azure/azure-sdk-for-python/pull/1")]
        [TestCase("not a URL")]
        public async Task MalformedCanonicalUrl_BlocksBeforeWrites(string url)
        {
            SetSdks(new SDKInfo { Language = "Python", SdkPullRequestUrl = url });
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Is.Not.Null);
            Assert.That(_github.Invocations, Is.Empty);
            NoWrites();
        }

        [TestCase("null")]
        [TestCase("unknown")]
        [TestCase("auth")]
        [TestCase("timeout")]
        public async Task UnreadablePullRequest_FailsClosed(string failure)
        {
            SetSdks(Sdk("Python", "azure-sdk-for-python", 1));
            var setup = _github.Setup(g => g.GetPullRequestAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()));
            if (failure == "auth")
            {
                setup.ThrowsAsync(new UnauthorizedAccessException("GitHub authentication failed"));
            }
            else if (failure == "timeout")
            {
                setup.ThrowsAsync(new OperationCanceledException("GitHub timeout"));
            }
            else
            {
                setup.ReturnsAsync(failure == "null" ? null! : Pr("unrecognized"));
            }
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Is.Not.Null);
            NoWrites();
        }

        [TestCase("Finished")]
        [TestCase("finished")]
        [TestCase("unknown")]
        [TestCase("")]
        public async Task FinishedOrUnknownState_Blocks(string status)
        {
            _plan.Status = status;
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Is.Not.Null);
            Assert.That(_devOps.Invocations.Any(i => i.Method.Name.StartsWith("Update", StringComparison.Ordinal)), Is.False);
            Assert.That(_github.Invocations, Is.Empty);
        }

        [TestCase("Abandoned")]
        [TestCase("abandoned")]
        public async Task AlreadyAbandoned_IsNoopEvenWithoutRevision(string status)
        {
            _plan.Status = status;
            _plan.Revision = 0;
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.Details.Single(), Does.Contain("already abandoned"));
            Assert.That(_devOps.Invocations.Count, Is.EqualTo(1));
            Assert.That(_github.Invocations, Is.Empty);
        }

        [TestCase("revision")]
        [TestCase("workitem")]
        [TestCase("planid")]
        [TestCase("state")]
        [TestCase("environment")]
        [TestCase("links")]
        [TestCase("released")]
        [TestCase("null")]
        public async Task RefreshChanges_RejectWithoutRetryOrLiveRead(string change)
        {
            switch (change)
            {
                case "revision": _fresh.Revision++; break;
                case "workitem": _fresh.WorkItemId++; break;
                case "planid": _fresh.ReleasePlanId++; break;
                case "state": _fresh.Status = "Finished"; break;
                case "environment": _fresh.IsTestReleasePlan = true; break;
                case "links": _fresh.SDKInfo.Add(Sdk("Python", "azure-sdk-for-python", 1)); break;
                case "released": _fresh.SDKInfo.Add(new SDKInfo { ReleaseStatus = "Released" }); break;
                case "null": _fresh = null!; break;
            }
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Is.Not.Null);
            Assert.That(_devOps.Invocations.Count, Is.EqualTo(2));
            Assert.That(_github.Invocations, Is.Empty);
            Assert.That(_devOps.Invocations.Any(i => i.Method.Name.StartsWith("Update", StringComparison.Ordinal)), Is.False);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task InvalidRevision_NoWrites(int revision)
        {
            _plan.Revision = revision;
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Is.Not.Null);
            NoWrites();
        }

        [TestCase("conflict")]
        [TestCase("null")]
        [TestCase("state")]
        public async Task UpdateFailure_NoSuccessOrRetry(string failure)
        {
            var setup = _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()));
            if (failure == "conflict")
            {
                setup.ThrowsAsync(new InvalidOperationException("revision conflict"));
            }
            else
            {
                setup.ReturnsAsync(failure == "null" ? null! : new WorkItem
                {
                    Id = 42, Fields = new Dictionary<string, object> { ["System.State"] = "In Progress" }
                });
            }
            var response = await _tool.AbandonReleasePlan(42);
            Assert.That(response.ResponseError, Is.Not.Null);
            Assert.That(response.Status, Is.Not.EqualTo("Success"));
            _devOps.Verify(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(_notifications.Invocations, Is.Empty);
        }

        [TestCase("before")]
        [TestCase("github")]
        [TestCase("refresh")]
        [TestCase("update")]
        [TestCase("saved-update")]
        public void CallerCancellation_Propagates(string phase)
        {
            using var cts = new CancellationTokenSource();
            if (phase == "before")
            {
                cts.Cancel();
            }
            else if (phase == "github")
            {
                SetSdks(Sdk("Python", "azure-sdk-for-python", 1));
                _github.Setup(g => g.GetPullRequestAsync("Azure", "azure-sdk-for-python", 1, cts.Token))
                    .Callback(() => cts.Cancel()).ThrowsAsync(new OperationCanceledException(cts.Token));
            }
            else if (phase == "refresh")
            {
                _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(42, cts.Token))
                    .Callback(() => cts.Cancel()).ReturnsAsync(_plan);
            }
            else if (phase == "saved-update")
            {
                _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, cts.Token))
                    .Callback(() => cts.Cancel()).ReturnsAsync(new WorkItem
                    {
                        Id = 42, Fields = new Dictionary<string, object> { ["System.State"] = "Abandoned" }
                    });
            }
            else
            {
                _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, cts.Token))
                    .Callback(() => cts.Cancel()).ThrowsAsync(new OperationCanceledException(cts.Token));
            }
            Assert.ThrowsAsync<OperationCanceledException>(async () => await _tool.AbandonReleasePlan(42, ct: cts.Token));
            if (phase != "update" && phase != "saved-update")
            {
                NoWrites();
            }
        }
    }
}
