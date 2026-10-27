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

        [SetUp]
        public void Setup()
        {
            _devOps = new Mock<IDevOpsService>(MockBehavior.Strict);
            _github = new Mock<IGitHubService>(MockBehavior.Strict);
            _notifications = new Mock<INotificationService>(MockBehavior.Strict);
            _plan = Plan();
            _devOps.Setup(s => s.GetReleasePlanAsync(123, It.IsAny<CancellationToken>(), true)).ReturnsAsync(() => _plan);
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
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task InvalidIds_NoReadsOrWrites(int releasePlanId)
        {
            var response = await _tool.AbandonReleasePlan(releasePlanId);
            Assert.That(response.ResponseError, Is.Not.Null);
            Assert.That(_devOps.Invocations, Is.Empty);
            NoWrites();
        }

        [Test]
        public async Task NoSdkLinks_DistinctPlanAndWorkItemIds_OneReadAndGuardedStateOnlyWrite()
        {
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.Status, Is.EqualTo("Success"));
            Assert.That(response.Details.Single(), Is.EqualTo("Release plan 123 has been successfully abandoned."));
            _devOps.Verify(s => s.UpdateWorkItemAsync(42,
                It.Is<Dictionary<string, string>>(f => f.Count == 1 && f["System.State"] == "Abandoned"),
                7, It.IsAny<CancellationToken>()), Times.Once);
            _devOps.Verify(s => s.GetReleasePlanAsync(123, It.IsAny<CancellationToken>(), true), Times.Once);
            Assert.That(_devOps.Invocations.Count, Is.EqualTo(2));
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
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Does.Contain("exception").And.Contain("unreleased").And.Contain("contact SDK Release Support channel on Teams"));
            NoWrites();
        }

        [TestCase("open", false)]
        [TestCase("open", true)]
        [TestCase("closed", false)]
        public async Task Eligibility_AllUnmergedMixedLanguages_AllowsOpenDraftAndClosed(string state, bool draft)
        {
            SetSdks(Sdk("c#", "azure-sdk-for-net", 1), Sdk("typescript", "azure-sdk-for-js", 2),
                Sdk("Python", "azure-sdk-for-python", 3), Sdk("Go", "azure-sdk-for-go", 4), Sdk("Java", "azure-sdk-for-java", 5));
            foreach (var sdk in _plan.SDKInfo)
            {
                var uri = new Uri(sdk.SdkPullRequestUrl);
                Lookup(uri.Segments[2].Trim('/'), int.Parse(uri.Segments[4]), Pr(state, draft: draft));
                sdk.PullRequestStatus = "Merged";
            }
            Assert.That(await _tool.IsEligibleToAbandonAsync(_plan), Is.True);
            Assert.That(_github.Invocations.Count, Is.EqualTo(5));
            Assert.That(_devOps.Invocations, Is.Empty);
            _github.Verify(g => g.IsPullRequestApprovedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task PrivateSdkRepository_BlocksBeforeRemoteReadOrWrite()
        {
            SetSdks(Sdk("Python", "azure-sdk-for-python-pr", 1));
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Does.Contain("public Azure SDK repository"));
            Assert.That(_github.Invocations, Is.Empty);
            NoWrites();
        }

        [TestCase(".NET", "azure-sdk-for-net")]
        [TestCase("dotnet", "azure-sdk-for-net")]
        [TestCase("c#", "azure-sdk-for-net")]
        [TestCase("csharp", "azure-sdk-for-net")]
        [TestCase("Java", "azure-sdk-for-java")]
        [TestCase("JavaScript", "azure-sdk-for-js")]
        [TestCase("js", "azure-sdk-for-js")]
        [TestCase("typescript", "azure-sdk-for-js")]
        [TestCase("Python", "azure-sdk-for-python")]
        [TestCase("Go", "azure-sdk-for-go")]
        [TestCase("Rust", "azure-sdk-for-rust")]
        [TestCase("C++", "azure-sdk-for-cpp")]
        [TestCase("cpp", "azure-sdk-for-cpp")]
        public async Task SupportedLanguageAliases_UseKnownPublicRepository(string language, string repo)
        {
            SetSdks(Sdk(language, repo, int.MaxValue));
            Lookup(repo, int.MaxValue, Pr());
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Is.Null);
            _github.Verify(g => g.GetPullRequestAsync("Azure", repo, int.MaxValue, It.IsAny<CancellationToken>()), Times.Once);
            _devOps.Verify(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("unsupported")]
        public async Task UnknownLinkedLanguage_BlocksBeforeRemoteReadOrWrite(string? language)
        {
            SetSdks(Sdk(language!, "azure-sdk-for-python", 1));
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Does.Contain("language"));
            Assert.That(_github.Invocations, Is.Empty);
            NoWrites();
        }

        [Test]
        public async Task InvalidLinkAfterValidLink_ValidatesAllBeforeRemoteReadOrWrite()
        {
            SetSdks(Sdk("Python", "azure-sdk-for-python", 1), Sdk("Java", "azure-sdk-for-java-pr", 2));
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Does.Contain("public Azure SDK repository"));
            Assert.That(_github.Invocations, Is.Empty);
            NoWrites();
        }

        [Test]
        public async Task Eligibility_MixedOpenDraftClosed_DeduplicatesCanonicalLinksAndIgnoresCachedStatuses()
        {
            SetSdks(Sdk(".NET", "azure-sdk-for-net", 1), Sdk("Python", "azure-sdk-for-python", 2),
                Sdk("JavaScript", "azure-sdk-for-js", 3), Sdk("Python", "azure-sdk-for-python", 2));
            _plan.SDKInfo[3].Language = " python ";
            _plan.SDKInfo[3].SdkPullRequestUrl = "HTTPS://GITHUB.COM/azure/AZURE-SDK-FOR-PYTHON/pull/0002/";
            Lookup("azure-sdk-for-net", 1, Pr());
            Lookup("azure-sdk-for-python", 2, Pr(draft: true));
            Lookup("azure-sdk-for-js", 3, Pr("closed"));
            Assert.That(await _tool.IsEligibleToAbandonAsync(_plan), Is.True);
            Assert.That(_github.Invocations.Count, Is.EqualTo(3));
            Assert.That(_devOps.Invocations, Is.Empty);
        }

        [TestCase("New")]
        [TestCase("Not Started")]
        [TestCase("In Progress")]
        public async Task KnownActiveStateWithoutSdkLinks_AllowsAbandonment(string status)
        {
            _plan.Status = status;
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Is.Null);
        }

        [TestCase("null-plan")]
        [TestCase("zero-workitem")]
        [TestCase("sdk-list")]
        [TestCase("sdk-entry")]
        public async Task MissingOrMismatchedPlanData_NoWrites(string invalid)
        {
            switch (invalid)
            {
                case "null-plan": _plan = null!; break;
                case "zero-workitem": _plan.WorkItemId = 0; break;
                case "sdk-list": _plan.SDKInfo = null!; break;
                case "sdk-entry": _plan.SDKInfo.Add(null!); break;
            }
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Is.Not.Null);
            Assert.That(_devOps.Invocations.Any(i => i.Method.Name.StartsWith("Update", StringComparison.Ordinal)), Is.False);
            Assert.That(_github.Invocations, Is.Empty);
        }

        [Test]
        public async Task ChangeDuringLiveRead_UsesOriginalRevisionAndNeverRetries()
        {
            SetSdks(Sdk("Python", "azure-sdk-for-python", 1));
            _github.Setup(g => g.GetPullRequestAsync("Azure", "azure-sdk-for-python", 1, It.IsAny<CancellationToken>()))
                .Callback(() => _plan.Revision++).ReturnsAsync(Pr());
            _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("ADO /rev conflict"));
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Does.Contain("/rev conflict"));
            _devOps.Verify(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()), Times.Once);
            _devOps.Verify(s => s.GetReleasePlanAsync(123, It.IsAny<CancellationToken>(), true), Times.Once);
            Assert.That(_devOps.Invocations.Count, Is.EqualTo(2));
            Assert.That(_notifications.Invocations, Is.Empty);
        }

        [TestCase("Released")]
        [TestCase("released")]
        [TestCase("RELEASED")]
        public async Task AnyRecordedRelease_BlocksWithoutGithubRead(string status)
        {
            SetSdks(new SDKInfo { Language = "Python", ReleaseStatus = status }, Sdk("Java", "azure-sdk-for-java", 1));
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Does.Contain("contact SDK Release Support channel on Teams"));
            Assert.That(_github.Invocations, Is.Empty);
            NoWrites();
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task Eligibility_AnyRecordedRelease_IsFalseWithoutLiveReads(int releasedIndex)
        {
            SetSdks(new SDKInfo { Language = ".NET" }, new SDKInfo { Language = "Python" }, new SDKInfo { Language = "Java" });
            _plan.SDKInfo[releasedIndex].ReleaseStatus = "Released";
            Assert.That(await _tool.IsEligibleToAbandonAsync(_plan), Is.False);
            Assert.That(_github.Invocations, Is.Empty);
            Assert.That(_devOps.Invocations, Is.Empty);
            NoWrites();
        }

        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/0")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/2147483648")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/9999999999999999999999999999999999")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/0000")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/-1")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/+1")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/1\n")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/1/\n")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/1//")]
        [TestCase("https://github.com/Azure/azure-sdk-for-python/pull/%31")]
        [TestCase("https://github.com/Other/azure-sdk-for-python/pull/1")]
        [TestCase("https://github.com:443/Azure/azure-sdk-for-python/pull/1")]
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
            var response = await _tool.AbandonReleasePlan(123);
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
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Is.Not.Null);
            NoWrites();
        }

        [TestCase("Finished")]
        [TestCase("finished")]
        [TestCase("unknown")]
        [TestCase("Closed")]
        [TestCase("Duplicate")]
        [TestCase("")]
        public async Task FinishedOrUnknownState_Blocks(string status)
        {
            _plan.Status = status;
            var response = await _tool.AbandonReleasePlan(123);
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
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.Details.Single(), Is.EqualTo("Release plan 123 is already abandoned; no state changes made."));
            Assert.That(_devOps.Invocations.Count, Is.EqualTo(1));
            Assert.That(_github.Invocations, Is.Empty);
        }

        [TestCase("Finished")]
        [TestCase("Abandoned")]
        public async Task Eligibility_FinishedOrAbandoned_IsFalseWithoutReadsOrWrites(string status)
        {
            _plan.Status = status;
            Assert.That(await _tool.IsEligibleToAbandonAsync(_plan), Is.False);
            Assert.That(_devOps.Invocations, Is.Empty);
            Assert.That(_github.Invocations, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task InvalidRevision_NoWrites(int revision)
        {
            _plan.Revision = revision;
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Is.Not.Null);
            NoWrites();
        }

        [TestCase("conflict")]
        [TestCase("null")]
        public async Task UpdateFailure_NoSuccessOrRetry(string failure)
        {
            var setup = _devOps.Setup(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()));
            if (failure == "conflict")
            {
                setup.ThrowsAsync(new InvalidOperationException("revision conflict"));
            }
            else
            {
                setup.ReturnsAsync((WorkItem)null!);
            }
            var response = await _tool.AbandonReleasePlan(123);
            Assert.That(response.ResponseError, Is.Not.Null);
            Assert.That(response.Status, Is.Not.EqualTo("Success"));
            _devOps.Verify(s => s.UpdateWorkItemAsync(42, It.IsAny<Dictionary<string, string>>(), 7, It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(_notifications.Invocations, Is.Empty);
        }

        [TestCase("before")]
        [TestCase("github")]
        [TestCase("lookup")]
        [TestCase("lookup-null")]
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
            else if (phase == "lookup" || phase == "lookup-null")
            {
                _devOps.Setup(s => s.GetReleasePlanAsync(123, cts.Token, true))
                    .Callback(() => cts.Cancel()).ReturnsAsync(phase == "lookup-null" ? null! : _plan);
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
            Assert.ThrowsAsync<OperationCanceledException>(async () => await _tool.AbandonReleasePlan(123, ct: cts.Token));
            if (phase != "update" && phase != "saved-update")
            {
                NoWrites();
            }
        }
    }
}
