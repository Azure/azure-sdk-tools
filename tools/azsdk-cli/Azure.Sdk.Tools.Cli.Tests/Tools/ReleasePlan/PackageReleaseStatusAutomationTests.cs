// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Services.Notification.Templates;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools.ReleasePlan
{
    [TestFixture]
    internal class PackageReleaseStatusAutomationTests
    {
        private const string PythonSdkPr = "https://github.com/Azure/azure-sdk-for-python/pull/100";
        private Mock<IDevOpsService> _devOps = null!;
        private Mock<INotificationService> _notifications = null!;
        private PackageReleaseStatusTool _tool = null!;
        private ReleasePlanWorkItem _plan = null!;
        private readonly List<string> _calls = [];
        private readonly List<(Dictionary<string, string> Fields, int Revision)> _writes = [];

        [SetUp]
        public void Setup()
        {
            _calls.Clear();
            _writes.Clear();
            _plan = new ReleasePlanWorkItem
            {
                WorkItemId = 12345,
                ReleasePlanId = 100,
                Revision = 7,
                Status = "In Progress",
                SDKReleaseType = "stable",
                IsManagementPlane = true,
                IsTestReleasePlan = bool.TryParse(Environment.GetEnvironmentVariable("AZSDKTOOLS_AGENT_TESTING"), out var testing) && testing,
                APISpecProjectPath = "specification/test/Contoso.Management",
                SpecAPIVersion = "2025-01-01",
                SDKInfo =
                [
                    new SDKInfo { Language = ".NET", PackageName = "Azure.Test", ReleaseStatus = "Released" },
                    new SDKInfo { Language = "Java", PackageName = "azure-test", ReleaseStatus = "Released" },
                    new SDKInfo { Language = "Python", PackageName = "azure-test", ReleaseStatus = "Pending", SdkPullRequestUrl = PythonSdkPr },
                    new SDKInfo { Language = "JavaScript", PackageName = "@azure/test", ReleaseStatus = "Released" },
                    new SDKInfo { Language = "Go", PackageName = "sdk/test/aztest", ReleaseStatus = "Released" }
                ]
            };
            _devOps = new Mock<IDevOpsService>();
            _devOps.Setup(s => s.GetReleasePlansByIdAsync(100, _plan.IsTestReleasePlan, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => [ReadPlanSnapshot()]);
            _devOps.Setup(s => s.GetReleasePlansBySdkPullRequestAsync(PythonSdkPr, "Python", _plan.IsTestReleasePlan, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _plan.Status == "In Progress" ? new List<ReleasePlanWorkItem> { ReadPlanSnapshot() } : []);
            _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>()))
                .Callback(() => _calls.Add("refresh"))
                .ReturnsAsync(ReadPlanSnapshot);
            _devOps.Setup(s => s.UpdateWorkItemAsync(12345, It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns((int id, Dictionary<string, string> fields, int revision, CancellationToken ct) =>
                    Task.FromResult(ApplyUpdate(id, fields, revision, ct)));
            _devOps.Setup(s => s.GetActiveReleasePlansByTypeSpecProjectPathAsync(It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            _devOps.Setup(s => s.EnsureReleasePlanAutomationRelationAsync(It.IsAny<int>(), 12345, It.IsAny<CancellationToken>()))
                .Callback(() => _calls.Add("relate"))
                .Returns(Task.CompletedTask);
            _devOps.Setup(s => s.RunPipelineAsync(8254, It.IsAny<Dictionary<string, string>>(), "main", It.IsAny<CancellationToken>()))
                .Callback(() => _calls.Add("queue"))
                .ReturnsAsync(new Build { Id = 9876 });
            _notifications = new Mock<INotificationService>();
            _notifications.Setup(s => s.SendEmailNotificationAsync(It.IsAny<EmailPayload>(), It.IsAny<CancellationToken>()))
                .Callback(() => _calls.Add("notify"))
                .ReturnsAsync(NotificationResult.Sent);
            _tool = new PackageReleaseStatusTool(_devOps.Object, new TestLogger<PackageReleaseStatusTool>(), _notifications.Object);
        }

        [TearDown]
        public void NeverUsesHeuristicLookupOrUnguardedWrites()
        {
            Assert.That(_devOps.Invocations.Where(i => i.Method.Name is nameof(IDevOpsService.ResolveReleasePlanByIdAsync)
                or nameof(IDevOpsService.GetReleasePlanAsync)), Is.Empty);
            Assert.That(_devOps.Invocations.Where(i => i.Method.Name == nameof(IDevOpsService.UpdateWorkItemAsync)
                && (i.Arguments.Count != 4 || i.Arguments[2] is not int)), Is.Empty);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_FinishedManagementPlan_QueuesNearestNewerReleasePlan()
        {
            ConfigureAutomation("2025-01-01-preview");
            var nearestNewerPlan = new ReleasePlanWorkItem
            {
                WorkItemId = 200,
                ReleasePlanId = 200,
                Status = "In Progress",
                APISpecProjectPath = _plan.APISpecProjectPath,
                SpecAPIVersion = "2025-01-01",
                ApiReleaseType = ApiReleaseType.GA
            };
            var laterPlan = new ReleasePlanWorkItem
            {
                WorkItemId = 300,
                ReleasePlanId = 300,
                Status = "In Progress",
                APISpecProjectPath = _plan.APISpecProjectPath,
                SpecAPIVersion = "2025-06-01-preview",
                ApiReleaseType = ApiReleaseType.PublicPreview
            };
            _devOps.Setup(s => s.GetActiveReleasePlansByTypeSpecProjectPathAsync(_plan.APISpecProjectPath, ApiReleaseType.Unknown, It.IsAny<CancellationToken>()))
                .ReturnsAsync([laterPlan, nearestNewerPlan]);

            var result = await UpdateAsync();

            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanFinished, Is.True);
            Assert.That(result.ReleasePlanAutomationTriggered, Is.True);
            Assert.That(result.QueuedReleasePlanId, Is.EqualTo(200));
            Assert.That(result.ReleasePlanAutomationPipelineUrl, Does.Contain("buildId=9876"));
            _devOps.Verify(s => s.EnsureReleasePlanAutomationRelationAsync(nearestNewerPlan.WorkItemId, _plan.WorkItemId, It.IsAny<CancellationToken>()), Times.Once);
            _notifications.Verify(s => s.SendEmailNotificationAsync(
                It.Is<EmailPayload>(email => email is ReleasePlanSdkGenerationEmail
                    && email.Body.Contains("?releaseplan=100")
                    && email.Body.Contains("?releaseplan=200")), It.IsAny<CancellationToken>()), Times.Once);
            _devOps.Verify(s => s.RunPipelineAsync(8254,
                It.Is<Dictionary<string, string>>(parameters => parameters.Count == 1 && parameters["ReleasePlanId"] == "200"),
                "main", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_FinishedDataPlane_DoesNotQueueAutomation()
        {
            var completed = ConfigureAutomation();
            completed.IsManagementPlane = false;
            completed.IsDataPlane = true;
            completed.SDKInfo.Single(s => s.Language == "Go").ReleaseStatus = "Pending";

            var result = await UpdateAsync();

            Assert.That(result.ReleasePlanFinished, Is.True);
            Assert.That(result.ReleasePlanAutomationTriggered, Is.False);
            VerifyNoCandidateLookup();
            VerifyNoAutomation();
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_AutomationFailure_PreservesFinishedResult()
        {
            ConfigureAutomation(nextVersion: "2025-02-01-preview");
            _devOps.Setup(s => s.RunPipelineAsync(8254, It.IsAny<Dictionary<string, string>>(), "main", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Pipeline unavailable"));

            var result = await UpdateAsync();

            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.ReleasePlanFinished, Is.True);
            Assert.That(result.ReleasePlanAutomationTriggered, Is.False);
            Assert.That(result.Message, Does.Contain("could not be queued"));
            Assert.That(result.NextSteps, Has.Some.Contains("azsdk agent"));
            _notifications.Verify(s => s.SendEmailNotificationAsync(
                It.Is<EmailPayload>(email => email.Subject.Contains("Action required")
                    && email.Body.Contains("unable to queue")
                    && email.Body.Contains("azsdk agent")
                    && email.Body.Contains("?releaseplan=200")), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_RejectsIneligibleAndInvalidCandidates()
        {
            ConfigureAutomation();
            _devOps.Setup(s => s.GetActiveReleasePlansByTypeSpecProjectPathAsync(_plan.APISpecProjectPath, ApiReleaseType.Unknown, It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new ReleasePlanWorkItem { WorkItemId = 101, ReleasePlanId = 101, Status = "Not Started", SpecAPIVersion = "2025-02-01", ApiReleaseType = ApiReleaseType.GA },
                    new ReleasePlanWorkItem { WorkItemId = 102, ReleasePlanId = 102, Status = "In Progress", SpecAPIVersion = "2025-02-01", ApiReleaseType = ApiReleaseType.PrivatePreview },
                    new ReleasePlanWorkItem { WorkItemId = 103, ReleasePlanId = 103, Status = "In Progress", SpecAPIVersion = "invalid", ApiReleaseType = ApiReleaseType.GA },
                    new ReleasePlanWorkItem { WorkItemId = 104, ReleasePlanId = 104, Status = "In Progress", SpecAPIVersion = "2024-12-01", ApiReleaseType = ApiReleaseType.GA }
                ]);

            var result = await UpdateAsync();

            Assert.That(result.ReleasePlanFinished, Is.True);
            Assert.That(result.ReleasePlanAutomationTriggered, Is.False);
            Assert.That(result.Warnings, Has.Some.Contains("103"));
            Assert.That(result.Message, Does.Contain("valid metadata"));
            VerifyNoAutomation();
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        public async Task UpdatePackageReleaseStatus_DisabledPlane_DoesNotQueue(bool management, bool data)
        {
            var completed = ConfigureAutomation();
            completed.IsManagementPlane = management;
            completed.IsDataPlane = data;

            var response = await UpdateAsync();

            Assert.That(response.ReleasePlanFinished, Is.True);
            Assert.That(response.Message, Does.Contain("only for management-plane"));
            VerifyNoAutomation();
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_LinksBeforeQueueingAndNotifiesAfterQueueing()
        {
            ConfigureAutomation();

            var response = await UpdateAsync();

            Assert.That(response.ReleasePlanAutomationTriggered, Is.True);
            Assert.That(_calls, Is.EqualTo(new[] { "release", "refresh", "finish", "relate", "queue", "notify" }));
            Assert.That(_writes.Select(w => w.Revision), Is.EqualTo(new[] { 7, 8 }));
            Assert.That(_writes[1].Fields, Is.EquivalentTo(new Dictionary<string, string> { ["System.State"] = "Finished" }));
            Assert.That(_plan.Status, Is.EqualTo("Finished"));
            _devOps.Verify(s => s.GetReleasePlansByIdAsync(100, _plan.IsTestReleasePlan, It.IsAny<CancellationToken>()), Times.Once);
            _devOps.Verify(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_RelationFailure_NotifiesWithoutQueueing()
        {
            ConfigureAutomation();
            _devOps.Setup(s => s.EnsureReleasePlanAutomationRelationAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Relation denied"));

            var response = await UpdateAsync();

            Assert.That(response.ReleasePlanFinished, Is.True);
            Assert.That(response.Message, Does.Contain("Relation denied"));
            Assert.That(response.NextSteps, Has.Some.Contains("azsdk agent"));
            _devOps.Verify(s => s.RunPipelineAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _notifications.Verify(s => s.SendEmailNotificationAsync(
                It.Is<EmailPayload>(email => email.Subject.Contains("Action required") && email.Body.Contains("unable to queue")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_LookupFailure_ReportsFailureWithoutQueueing()
        {
            ConfigureAutomation();
            _devOps.Setup(s => s.GetActiveReleasePlansByTypeSpecProjectPathAsync(It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Lookup unavailable"));

            var response = await UpdateAsync();

            Assert.That(response.ReleasePlanFinished, Is.True);
            Assert.That(response.Message, Does.Contain("Lookup unavailable"));
            Assert.That(response.NextSteps, Has.Some.Contains("azsdk agent"));
            VerifyNoAutomation();
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_NoCandidates_IsSuccessfulNoOp()
        {
            ConfigureAutomation();
            _devOps.Setup(s => s.GetActiveReleasePlansByTypeSpecProjectPathAsync(It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            var response = await UpdateAsync();

            Assert.That(response.Message, Does.Contain("No newer"));
            Assert.That(response.Warnings, Is.Null);
            Assert.That(response.NextSteps, Is.Null);
            VerifyNoAutomation();
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_NotificationFailure_DoesNotMisreportQueueFailure()
        {
            ConfigureAutomation();
            _notifications.Setup(s => s.SendEmailNotificationAsync(It.IsAny<EmailPayload>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new NotificationResult(NotificationStatus.Failed, "Email unavailable"));

            var response = await UpdateAsync();

            Assert.That(response.ReleasePlanAutomationTriggered, Is.True);
            Assert.That(response.ReleasePlanAutomationPipelineUrl, Does.Contain("buildId=9876"));
            Assert.That(response.Warnings, Has.Some.Contains("notification failed"));
            Assert.That(response.Warnings, Has.Some.Contains("Email unavailable"));
            Assert.That(response.NextSteps, Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task UpdatePackageReleaseStatus_QueueOutcome_IsAccurateInPlainAndJson(bool queued)
        {
            ConfigureAutomation();
            if (!queued)
            {
                _devOps.Setup(s => s.RunPipelineAsync(8254, It.IsAny<Dictionary<string, string>>(), "main", It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("Queue unavailable"));
            }

            var response = await UpdateAsync();

            using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
            Assert.That(json.RootElement.GetProperty("release_plan_finished").GetBoolean(), Is.True);
            if (queued)
            {
                Assert.That(json.RootElement.GetProperty("release_plan_automation_triggered").GetBoolean(), Is.True);
                Assert.That(json.RootElement.GetProperty("queued_release_plan_id").GetInt32(), Is.EqualTo(200));
                Assert.That(json.RootElement.GetProperty("release_plan_automation_pipeline_url").GetString(), Does.Contain("buildId=9876"));
                Assert.That(response.ToString(), Does.Contain("Queued release plan 200"));
            }
            else
            {
                Assert.That(json.RootElement.TryGetProperty("release_plan_automation_triggered", out _), Is.False);
                Assert.That(json.RootElement.TryGetProperty("queued_release_plan_id", out _), Is.False);
                Assert.That(json.RootElement.TryGetProperty("release_plan_automation_pipeline_url", out _), Is.False);
                Assert.That(response.ToString(), Does.Contain("Queue unavailable").And.Contain("azsdk agent"));
                Assert.That(response.ToString(), Does.Not.Contain("Queued release plan"));
            }
            _notifications.Verify(s => s.SendEmailNotificationAsync(
                It.Is<EmailPayload>(email => email.Subject.StartsWith("Action required") == !queued), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task UpdatePackageReleaseStatus_MissingProjectPath_ReportsMetadataError()
        {
            var completed = ConfigureAutomation();
            completed.APISpecProjectPath = "";

            var response = await UpdateAsync();

            Assert.That(response.ReleasePlanFinished, Is.True);
            Assert.That(response.Message, Does.Contain("no TypeSpec project path"));
            VerifyNoCandidateLookup();
            VerifyNoAutomation();
        }

        [TestCase("2025-01-01", "2025-02-01", true)]
        [TestCase("2025-02-01", "2025-01-01", false)]
        [TestCase("2025-01-01-preview", "2025-01-01", true)]
        [TestCase("2025-01-01", "2025-01-01-preview", false)]
        [TestCase("2025-01-01", "2025-01-01", false)]
        [TestCase("2025-01-01-preview", "2025-01-01-preview", false)]
        [TestCase("2025-01-01", "2025-02-01-preview", true)]
        [TestCase("2024-12-31", "2025-01-01-preview", true)]
        [TestCase("2024-02-28", "2024-02-29", true)]
        public async Task UpdatePackageReleaseStatus_VersionOrdering(string current, string next, bool expected)
        {
            ConfigureAutomation(current, next);

            var response = await UpdateAsync();

            Assert.That(response.ReleasePlanFinished, Is.True);
            Assert.That(response.ReleasePlanAutomationTriggered, Is.EqualTo(expected));
            if (!expected)
            {
                VerifyNoAutomation();
            }
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("invalid")]
        [TestCase("2025-02-29")]
        [TestCase("2025-2-01")]
        [TestCase("2025-02-01-beta")]
        public async Task UpdatePackageReleaseStatus_InvalidCandidateVersion_IsVisibleInPlainAndJson(string? version)
        {
            ConfigureAutomation(nextVersion: version!);

            var response = await UpdateAsync();

            Assert.That(response.ReleasePlanFinished, Is.True);
            Assert.That(response.Warnings, Has.Some.Contains("Skipped release plan 200"));
            Assert.That(response.ToString(), Does.Contain("[WARNING]"));
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
            Assert.That(json.RootElement.GetProperty("warnings").GetArrayLength(), Is.EqualTo(1));
            Assert.That(response.Message, Does.Contain("correct the release plan metadata"));
            VerifyNoAutomation();
        }

        [TestCase("")]
        [TestCase("invalid")]
        [TestCase("2025-02-29")]
        public async Task UpdatePackageReleaseStatus_InvalidCompletedVersion_ReportsMetadataError(string version)
        {
            ConfigureAutomation(currentVersion: version);

            var response = await UpdateAsync();

            Assert.That(response.ResponseError, Is.Null, "API versions order follow-up generation, not status correlation.");
            Assert.That(response.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(response.ReleasePlanFinished, Is.True);
            Assert.That(response.Message, Does.Contain("invalid API version"));
            Assert.That(response.Message, Does.Not.Contain("No newer"));
            Assert.That(response.NextSteps, Has.Some.Contains("azsdk agent"));
            VerifyNoAutomation();
        }

        [TestCase("finish")]
        [TestCase("lookup")]
        [TestCase("queue")]
        public void UpdatePackageReleaseStatus_Cancellation_Propagates(string stage)
        {
            var completed = ConfigureAutomation();
            using var cts = new CancellationTokenSource();
            if (stage == "finish")
            {
                _devOps.Setup(s => s.UpdateWorkItemAsync(completed.WorkItemId,
                    It.Is<Dictionary<string, string>>(fields => fields.ContainsKey("System.State")), 8, cts.Token))
                    .Callback(() => cts.Cancel()).ThrowsAsync(new OperationCanceledException(cts.Token));
            }
            else if (stage == "lookup")
            {
                _devOps.Setup(s => s.GetActiveReleasePlansByTypeSpecProjectPathAsync(completed.APISpecProjectPath, ApiReleaseType.Unknown, cts.Token))
                    .Callback(() => cts.Cancel()).ThrowsAsync(new OperationCanceledException(cts.Token));
            }
            else
            {
                _devOps.Setup(s => s.RunPipelineAsync(8254, It.IsAny<Dictionary<string, string>>(), "main", cts.Token))
                    .Callback(() => cts.Cancel()).ThrowsAsync(new OperationCanceledException(cts.Token));
            }

            Assert.ThrowsAsync<OperationCanceledException>(() => UpdateAsync(ct: cts.Token));

            _notifications.Verify(s => s.SendEmailNotificationAsync(It.IsAny<EmailPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestCase("release")]
        [TestCase("refresh")]
        [TestCase("finish")]
        public async Task UpdatePackageReleaseStatus_UnsuccessfulCompletion_DoesNotQueueAutomation(string stage)
        {
            ConfigureAutomation();
            if (stage == "refresh")
            {
                _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("Refresh unavailable"));
            }
            else
            {
                _devOps.Setup(s => s.UpdateWorkItemAsync(12345,
                    It.Is<Dictionary<string, string>>(fields => fields.ContainsKey("System.State") == (stage == "finish")),
                    It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("Revision conflict"));
            }

            var response = await UpdateAsync();

            Assert.That(response.ReleasePlanFinished, Is.False);
            Assert.That(response.ReleasePlanAutomationTriggered, Is.False);
            Assert.That(response.ResponseError is null, Is.EqualTo(stage != "release"));
            Assert.That(response.ReleaseStatus, Is.EqualTo(stage == "release" ? "" : "Released"));
            Assert.That(_writes, Has.Count.EqualTo(stage == "release" ? 0 : 1));
            if (stage != "release")
            {
                Assert.That(response.Message, Does.Contain("failed to auto-finish"));
            }
            VerifyNoCandidateLookup();
            VerifyNoAutomation();
        }

        [TestCase(false, "refresh")]
        [TestCase(true, "refresh")]
        [TestCase(false, "finish")]
        [TestCase(true, "finish")]
        public async Task CompletionRetry_QueuesOnceAfterRecovery_AndFinishedRetryDoesNotQueue(bool automatic, string failedStage)
        {
            ConfigureAutomation();
            if (failedStage == "refresh")
            {
                _devOps.SetupSequence(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("Temporary refresh failure"))
                    .Returns(() => Task.FromResult(ReadPlanSnapshot()));
            }
            else
            {
                var attempts = 0;
                _devOps.Setup(s => s.UpdateWorkItemAsync(12345,
                    It.Is<Dictionary<string, string>>(fields => fields.ContainsKey("System.State")), 8, It.IsAny<CancellationToken>()))
                    .Returns((int id, Dictionary<string, string> fields, int revision, CancellationToken ct) =>
                        ++attempts == 1
                            ? Task.FromException<WorkItem>(new InvalidOperationException("Temporary Finished write failure"))
                            : Task.FromResult(ApplyUpdate(id, fields, revision, ct)));
            }

            var initial = await UpdateAsync(automatic, pipeline: "https://example.test/build/1");

            Assert.That(initial.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(initial.ReleasePlanFinished, Is.False);
            Assert.That(initial.Message, Does.Contain("failed to auto-finish"));
            VerifyNoCandidateLookup();
            VerifyNoAutomation();

            var retry = await UpdateAsync(automatic, pipeline: "https://example.test/build/2");
            var alreadyFinished = await UpdateAsync();

            Assert.That(retry.ResponseError, Is.Null);
            Assert.That(retry.ReleasePlanFinished, Is.True);
            Assert.That(retry.ReleasePlanAutomationTriggered, Is.True);
            Assert.That(alreadyFinished.ResponseError, Is.Null);
            Assert.That(alreadyFinished.Message, Does.Contain("already marked Released"));
            Assert.That(alreadyFinished.ReleasePlanAutomationTriggered, Is.False);
            Assert.That(_writes, Has.Count.EqualTo(2));
            Assert.That(_writes[0].Fields["Custom.ReleasePipelineForPython"], Is.EqualTo("https://example.test/build/1"));
            Assert.That(_writes[1].Fields, Is.EquivalentTo(new Dictionary<string, string> { ["System.State"] = "Finished" }));
            Assert.That(_writes[1].Revision, Is.EqualTo(8));
            Assert.That(_plan.Status, Is.EqualTo("Finished"));
            Assert.That(_plan.Revision, Is.EqualTo(9));
            _devOps.Verify(s => s.UpdateWorkItemAsync(12345,
                It.Is<Dictionary<string, string>>(fields => fields.ContainsKey("Custom.ReleaseStatusForPython")),
                It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
            _devOps.Verify(s => s.UpdateWorkItemAsync(12345,
                It.Is<Dictionary<string, string>>(fields => fields.Count == 1 && fields["System.State"] == "Finished"),
                8, It.IsAny<CancellationToken>()), Times.Exactly(failedStage == "finish" ? 2 : 1));
            _devOps.Verify(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>()), Times.Exactly(2));
            _devOps.Verify(s => s.GetActiveReleasePlansByTypeSpecProjectPathAsync(_plan.APISpecProjectPath, ApiReleaseType.Unknown, It.IsAny<CancellationToken>()), Times.Once);
            _devOps.Verify(s => s.EnsureReleasePlanAutomationRelationAsync(23456, 12345, It.IsAny<CancellationToken>()), Times.Once);
            _devOps.Verify(s => s.RunPipelineAsync(8254, It.IsAny<Dictionary<string, string>>(), "main", It.IsAny<CancellationToken>()), Times.Once);
            _notifications.Verify(s => s.SendEmailNotificationAsync(It.IsAny<EmailPayload>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task CompletionRefresh_UsesFreshApiVersionOnlyForFollowUpSelection()
        {
            ConfigureAutomation();
            _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    _plan.SpecAPIVersion = "2025-03-01";
                    _plan.Revision = 12;
                })
                .ReturnsAsync(ReadPlanSnapshot);
            _devOps.Setup(s => s.GetActiveReleasePlansByTypeSpecProjectPathAsync(_plan.APISpecProjectPath, ApiReleaseType.Unknown, It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new ReleasePlanWorkItem { WorkItemId = 23456, ReleasePlanId = 200, Status = "In Progress", SpecAPIVersion = "2025-02-01", ApiReleaseType = ApiReleaseType.GA },
                    new ReleasePlanWorkItem { WorkItemId = 34567, ReleasePlanId = 300, Status = "In Progress", SpecAPIVersion = "2025-04-01", ApiReleaseType = ApiReleaseType.GA }
                ]);

            var response = await UpdateAsync();

            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.ReleasePlanFinished, Is.True);
            Assert.That(response.QueuedReleasePlanId, Is.EqualTo(300), "Use the fresh completed plan, not the original correlation snapshot.");
            Assert.That(_writes.Select(w => w.Revision), Is.EqualTo(new[] { 7, 12 }));
            _devOps.Verify(s => s.RunPipelineAsync(8254,
                It.Is<Dictionary<string, string>>(parameters => parameters["ReleasePlanId"] == "300"), "main", It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestCase("package")]
        [TestCase("revision")]
        [TestCase("incomplete")]
        [TestCase("Abandoned")]
        [TestCase("Finished")]
        public async Task CompletionRefresh_ChangedOrNonActivePlan_DoesNotQueueAutomation(string change)
        {
            ConfigureAutomation();
            _devOps.Setup(s => s.GetReleasePlanForWorkItemAsync(12345, It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    switch (change)
                    {
                        case "package": _plan.SDKInfo.Single(s => s.Language == "Python").PackageName = "another-package"; break;
                        case "revision": _plan.Revision = 0; break;
                        case "incomplete": _plan.SDKInfo.Single(s => s.Language == "Java").ReleaseStatus = "Pending"; break;
                        default: _plan.Status = change; break;
                    }
                })
                .ReturnsAsync(ReadPlanSnapshot);

            var response = await UpdateAsync();

            Assert.That(response.ResponseError, Is.Null);
            Assert.That(response.ReleaseStatus, Is.EqualTo("Released"));
            Assert.That(response.ReleasePlanFinished, Is.False);
            Assert.That(_writes, Has.Count.EqualTo(1));
            VerifyNoCandidateLookup();
            VerifyNoAutomation();
        }

        private ReleasePlanWorkItem ConfigureAutomation(string currentVersion = "2025-01-01", string nextVersion = "2025-02-01")
        {
            _plan.SpecAPIVersion = currentVersion;
            var next = new ReleasePlanWorkItem
            {
                WorkItemId = 23456,
                ReleasePlanId = 200,
                Status = "In Progress",
                IsManagementPlane = true,
                IsTestReleasePlan = _plan.IsTestReleasePlan,
                APISpecProjectPath = _plan.APISpecProjectPath,
                ApiReleaseType = ApiReleaseType.PublicPreview,
                SpecAPIVersion = nextVersion,
                ReleasePlanSubmittedByEmail = "submitter@microsoft.com"
            };
            _devOps.Setup(s => s.GetActiveReleasePlansByTypeSpecProjectPathAsync(_plan.APISpecProjectPath, ApiReleaseType.Unknown, It.IsAny<CancellationToken>()))
                .ReturnsAsync([next]);
            return _plan;
        }

        private Task<ReleaseStatusUpdateResponse> UpdateAsync(bool automatic = false, string? pipeline = null, CancellationToken ct = default) =>
            _tool.UpdatePackageReleaseStatus("azure-test", "Python", "Released", "1.2.3",
                releasePlanId: automatic ? 0 : 100, releasePipelineUrl: pipeline, sdkPullRequest: automatic ? PythonSdkPr : null, ct: ct);

        private WorkItem ApplyUpdate(int id, Dictionary<string, string> fields, int revision, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (id != _plan.WorkItemId || revision != _plan.Revision)
            {
                throw new InvalidOperationException("Work item revision conflict.");
            }
            _writes.Add((new Dictionary<string, string>(fields), revision));
            _calls.Add(fields.ContainsKey("System.State") ? "finish" : "release");
            var python = _plan.SDKInfo.Single(s => s.Language == "Python");
            if (fields.TryGetValue("Custom.ReleaseStatusForPython", out var status)) python.ReleaseStatus = status;
            if (fields.TryGetValue("Custom.ReleasedVersionForPython", out var version)) python.ReleasedVersion = version;
            if (fields.TryGetValue("System.State", out var state)) _plan.Status = state;
            _plan.Revision++;
            return new WorkItem { Id = id, Rev = _plan.Revision };
        }

        // Each read is a detached snapshot; only successful guarded writes change the stored plan.
        private ReleasePlanWorkItem ReadPlanSnapshot() => new()
        {
            WorkItemId = _plan.WorkItemId,
            ReleasePlanId = _plan.ReleasePlanId,
            Revision = _plan.Revision,
            Status = _plan.Status,
            SDKReleaseType = _plan.SDKReleaseType,
            IsManagementPlane = _plan.IsManagementPlane,
            IsDataPlane = _plan.IsDataPlane,
            IsTestReleasePlan = _plan.IsTestReleasePlan,
            APISpecProjectPath = _plan.APISpecProjectPath,
            SpecAPIVersion = _plan.SpecAPIVersion,
            SDKInfo = _plan.SDKInfo.Select(s => new SDKInfo
            {
                Language = s.Language,
                PackageName = s.PackageName,
                ReleaseStatus = s.ReleaseStatus,
                ReleasedVersion = s.ReleasedVersion,
                ReleaseExclusionStatus = s.ReleaseExclusionStatus,
                SdkPullRequestUrl = s.SdkPullRequestUrl
            }).ToList()
        };

        private void VerifyNoCandidateLookup() =>
            _devOps.Verify(s => s.GetActiveReleasePlansByTypeSpecProjectPathAsync(It.IsAny<string>(), It.IsAny<ApiReleaseType>(), It.IsAny<CancellationToken>()), Times.Never);

        private void VerifyNoAutomation()
        {
            _devOps.Verify(s => s.EnsureReleasePlanAutomationRelationAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _devOps.Verify(s => s.RunPipelineAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _notifications.Verify(s => s.SendEmailNotificationAsync(It.IsAny<EmailPayload>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
