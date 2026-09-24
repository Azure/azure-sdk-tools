// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net.Http;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using DevOpsPatchOperation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    [TestFixture]
    public class SdkGenerationCompletionTests
    {
        private const int WorkItemId = 35000;
        private const int ApiSpecWorkItemId = 35001;
        private const int BuildId = 99;
        private const int ParentRevision = 7;
        private const string SpecCommit = "0123456789abcdef0123456789abcdef01234567";
        private const string OtherSpecCommit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string ApiVersion = "2026-01-01-preview";
        private const string ProjectPath = "specification/contoso/Contoso.Management";
        private const string SdkPullRequest = "https://github.com/Azure/azure-sdk-for-java/pull/123";
        private const string PreviousSdkPullRequest = "https://github.com/Azure/azure-sdk-for-java/pull/100";

        private Mock<BuildHttpClient> _buildClient = null!;
        private Mock<IDevOpsConnection> _connection = null!;
        private SdkGenerationWorkItemClient _workItems = null!;
        private DevOpsService _service = null!;
        private Build _build = null!;

        [SetUp]
        public void SetUp()
        {
            _build = new Build
            {
                Id = BuildId,
                Definition = new DefinitionReference { Id = 7421 },
                SourceBranch = "refs/heads/main",
                SourceVersion = SpecCommit,
                TemplateParameters = new Dictionary<string, string>
                {
                    ["ConfigType"] = "TypeSpec",
                    ["ConfigPath"] = $"{ProjectPath}/tspconfig.yaml",
                    ["CreatePullRequest"] = "true",
                    ["ReleasePlanWorkItemId"] = WorkItemId.ToString(CultureInfo.InvariantCulture),
                    ["TriggerSource"] = "sdk-review",
                    ["SdkReleaseType"] = "beta",
                    ["ApiVersion"] = ApiVersion
                }
            };
            _workItems = new SdkGenerationWorkItemClient();
            _workItems.AddWorkItem(CreateParent());
            _workItems.AddWorkItem(new WorkItem
            {
                Id = ApiSpecWorkItemId,
                Rev = 3,
                Fields = new Dictionary<string, object>
                {
                    ["System.WorkItemType"] = "API Spec",
                    ["Custom.APISpecversion"] = ApiVersion,
                    ["Custom.APISpecDefinitionType"] = "TypeSpec",
                    ["Custom.ActiveSpecPullRequestUrl"] = "https://github.com/Azure/azure-rest-api-specs/pull/123"
                }
            });
            _buildClient = new Mock<BuildHttpClient>(new Uri("https://dev.azure.com/test"), new VssCredentials());
            _buildClient.Setup(client => client.GetBuildAsync("internal", BuildId, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _build);
            _connection = new Mock<IDevOpsConnection>(MockBehavior.Strict);
            _connection.Setup(connection => connection.GetBuildClient(It.IsAny<CancellationToken>())).Returns(_buildClient.Object);
            _connection.Setup(connection => connection.GetWorkItemClient(It.IsAny<CancellationToken>())).Returns(_workItems);
            _service = new DevOpsService(new TestLogger<DevOpsService>(), _connection.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _buildClient.Object.Dispose();
            _workItems.Dispose();
        }

        [TestCase("Java", 7421)]
        [TestCase("Python", 7423)]
        [TestCase(".NET", 7412)]
        [TestCase("JavaScript", 7422)]
        [TestCase("Go", 7426)]
        [TestCase("nEt", 7412)]
        [TestCase("dOtNeT", 7412)]
        [TestCase("cSHARP", 7412)]
        [TestCase("jS", 7422)]
        [TestCase("tYpeScript", 7422)]
        public async Task ValidateRun_MatchingTarget_ReturnsOriginalBuildWithoutWrites(string language, int definitionId)
        {
            _build.Definition.Id = definitionId;
            using var cancellation = new CancellationTokenSource();

            var result = await _service.ValidateSdkGenerationRunAsync(WorkItemId, BuildId, language, cancellation.Token);

            Assert.That(result, Is.SameAs(_build));
            AssertNoWrites();
            _buildClient.Verify(client => client.GetBuildAsync("internal", BuildId, null, null, cancellation.Token), Times.Once);
            Assert.That(_workItems.Reads.All(read => read.Token == cancellation.Token), Is.True);
        }

        [TestCase("Java", 7421, "Java", "java")]
        [TestCase("Python", 7423, "Python", "python")]
        [TestCase(".NET", 7412, "Dotnet", "net")]
        [TestCase("JavaScript", 7422, "JavaScript", "js")]
        [TestCase("Go", 7426, "Go", "go")]
        [TestCase("nEt", 7412, "Dotnet", "net")]
        [TestCase("jS", 7422, "JavaScript", "js")]
        public async Task CompleteRun_MatchingTarget_WritesOnlyExpectedFieldsAndFirstSnapshotGuards(
            string language, int definitionId, string fieldLanguage, string repositorySuffix)
        {
            _build.Definition.Id = definitionId;
            var pullRequest = $"https://github.com/Azure/azure-sdk-for-{repositorySuffix}/pull/123";
            using var cancellation = new CancellationTokenSource();

            var result = await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, language, pullRequest, "draft", cancellation.Token);

            Assert.That(result, Is.True);
            Assert.That(_workItems.SuccessfulUpdates, Is.EqualTo(1));
            Assert.That(_workItems.UpdateAttempts, Has.Count.EqualTo(1));
            var update = _workItems.UpdateAttempts.Single();
            Assert.That(update.Id, Is.EqualTo(WorkItemId));
            Assert.That(update.Token, Is.EqualTo(cancellation.Token));
            Assert.That(update.Patch.Select(operation => (operation.Operation, operation.Path, operation.Value)), Is.EqualTo(new (DevOpsPatchOperation, string, object)[]
            {
                (DevOpsPatchOperation.Test, "/rev", ParentRevision),
                (DevOpsPatchOperation.Test, $"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}", SpecCommit),
                (DevOpsPatchOperation.Test, $"/fields/Custom.SDKGenerationPipelineFor{fieldLanguage}", DevOpsService.GetPipelineUrl(BuildId)),
                (DevOpsPatchOperation.Add, $"/fields/Custom.SDKPullRequestFor{fieldLanguage}", pullRequest),
                (DevOpsPatchOperation.Add, $"/fields/Custom.SDKPullRequestStatusFor{fieldLanguage}", "draft"),
                (DevOpsPatchOperation.Add, $"/fields/Custom.GenerationStatusFor{fieldLanguage}", "Completed")
            }));
            Assert.That(_workItems.Reads[0], Is.EqualTo((WorkItemId, (WorkItemExpand?)WorkItemExpand.All, cancellation.Token)));
            Assert.That(_workItems.Reads.All(read => read.Token == cancellation.Token), Is.True);
            Assert.That(_workItems.GetStoredWorkItem(WorkItemId).Rev, Is.EqualTo(ParentRevision + 1));
            _buildClient.Verify(client => client.GetBuildAsync("internal", BuildId, null, null, cancellation.Token), Times.Once);
        }

        [Test]
        public void GetMismatch_IsPureAndComparesLanguageAndLabelsCaseInsensitively()
        {
            var plan = new ReleasePlanWorkItem
            {
                WorkItemId = WorkItemId,
                SpecCommitSHA = SpecCommit.ToUpperInvariant(),
                SpecAPIVersion = ApiVersion.ToUpperInvariant(),
                SDKReleaseType = "BETA",
                APISpecProjectPath = ProjectPath.Replace('/', '\\') + "\\",
                ApiReleaseType = ApiReleaseType.PublicPreview,
                SDKInfo = [new SDKInfo { Language = "jAvA", GenerationPipelineUrl = DevOpsService.GetPipelineUrl(BuildId) }]
            };

            Assert.That(SdkGenerationTargetHelper.GetMismatch(_build, plan, "JAVA"), Is.Null);
            Assert.That(_workItems.Reads, Is.Empty);
            AssertNoWrites();

            _build.SourceVersion = OtherSpecCommit;
            var mismatch = SdkGenerationTargetHelper.GetMismatch(_build, plan, "Java");
            Assert.That(mismatch, Does.Contain($"expected '{plan.SpecCommitSHA}'"));
            Assert.That(mismatch, Does.Contain($"actual '{OtherSpecCommit}'"));
        }

        [Test]
        public async Task CompleteRun_NoChangesPreservesExistingPullRequestAndStatus()
        {
            var before = _workItems.GetStoredWorkItem(WorkItemId);

            Assert.That(await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", "", "No changes", CancellationToken.None), Is.True);

            var patch = _workItems.UpdateAttempts.Single().Patch;
            Assert.That(patch.Count, Is.EqualTo(4));
            Assert.That(patch.Last().Path, Is.EqualTo("/fields/Custom.GenerationStatusForJava"));
            Assert.That(patch.Last().Value, Is.EqualTo("Completed"));
            Assert.That(patch.Any(operation => operation.Path.StartsWith("/fields/Custom.SDKPullRequest", StringComparison.Ordinal)), Is.False);
            Assert.That(_workItems.GetStoredWorkItem(WorkItemId).Fields["Custom.SDKPullRequestForJava"], Is.EqualTo(before.Fields["Custom.SDKPullRequestForJava"]));
        }

        [Test]
        public async Task GetMismatch_RequiresRequestedSdkEntryAndIgnoresOtherReleasedLanguages()
        {
            _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields["Custom.ReleaseStatusForPython"] = "Released");
            var plan = await _service.GetReleasePlanForWorkItemAsync(WorkItemId, CancellationToken.None);

            Assert.That(SdkGenerationTargetHelper.GetMismatch(_build, plan, "Java"), Is.Null);
            plan.SDKInfo.RemoveAll(info => info.Language == "Java");
            Assert.That(SdkGenerationTargetHelper.GetMismatch(_build, plan, "Java"), Does.Contain("SDKInfo.Language"));
            AssertNoWrites();
        }

        [TestCase("")]
        [TestCase("Ruby")]
        public void UnsupportedLanguage_IsRejectedWithoutWrites(string language)
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.ValidateSdkGenerationRunAsync(WorkItemId, BuildId, language, CancellationToken.None));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, language, "", "Failed to generate SDK.", CancellationToken.None));
            AssertNoWrites();
        }

        [TestCase("build-id", "build ID")]
        [TestCase("definition-id", "definition ID")]
        [TestCase("missing-definition", "definition ID")]
        [TestCase("source-sha", "Build.SourceVersion")]
        [TestCase("invalid-source-sha", "Build.SourceVersion")]
        [TestCase("missing-source-sha", "Build.SourceVersion")]
        [TestCase("template-api-version", "ApiVersion")]
        [TestCase("template-release-type", "SdkReleaseType")]
        [TestCase("template-project", "ConfigPath")]
        [TestCase("template-project-case", "ConfigPath")]
        [TestCase("template-config-type", "ConfigType")]
        [TestCase("template-work-item-id", "ReleasePlanWorkItemId")]
        [TestCase("missing-ApiVersion", "ApiVersion")]
        [TestCase("missing-SdkReleaseType", "SdkReleaseType")]
        [TestCase("missing-ConfigPath", "ConfigPath")]
        [TestCase("missing-ConfigType", "ConfigType")]
        [TestCase("missing-ReleasePlanWorkItemId", "ReleasePlanWorkItemId")]
        [TestCase("missing-template-parameters", "ApiVersion")]
        [TestCase("stored-sha", "Build.SourceVersion")]
        [TestCase("missing-stored-sha", "SpecCommitSHA")]
        [TestCase("invalid-stored-sha", "SpecCommitSHA")]
        [TestCase("transition-stored-sha", "SpecCommitSHA")]
        [TestCase("stored-api-version", "ApiVersion")]
        [TestCase("missing-stored-api-version", "SpecAPIVersion")]
        [TestCase("none-stored-api-version", "SpecAPIVersion")]
        [TestCase("stored-release-type", "SdkReleaseType")]
        [TestCase("missing-stored-release-type", "SDKReleaseType")]
        [TestCase("stored-project", "ConfigPath")]
        [TestCase("missing-stored-project", "APISpecProjectPath")]
        [TestCase("latest-url", "GenerationPipelineUrl")]
        [TestCase("missing-latest-url", "GenerationPipelineUrl")]
        [TestCase("foreign-latest-url", "GenerationPipelineUrl")]
        [TestCase("released-sdk", "ReleaseStatus")]
        [TestCase("private-plan", "ApiReleaseType")]
        [TestCase("missing-child", "SpecCommitSHA")]
        public void ChangedOrMissingSnapshots_RejectValidationAndCompletionWithoutWrites(string scenario, string field)
        {
            ChangeSnapshot(scenario);

            var validation = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.ValidateSdkGenerationRunAsync(WorkItemId, BuildId, "Java", CancellationToken.None));
            var completion = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", SdkPullRequest, "draft", CancellationToken.None));

            foreach (var error in new[] { validation!, completion! })
            {
                Assert.That(error.Message, Does.Contain(field).IgnoreCase);
                Assert.That(error.Message, Does.Contain("expected").IgnoreCase);
                Assert.That(error.Message, Does.Contain("actual").IgnoreCase);
            }
            AssertNoWrites();
        }

        [TestCase(0, BuildId)]
        [TestCase(-1, BuildId)]
        [TestCase(WorkItemId, 0)]
        [TestCase(WorkItemId, -1)]
        public void NonPositiveIds_AreRejectedBeforeReads(int workItemId, int buildId)
        {
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await _service.ValidateSdkGenerationRunAsync(workItemId, buildId, "Java", CancellationToken.None));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await _service.CompleteSdkGenerationAsync(workItemId, buildId, "Java", SdkPullRequest, "draft", CancellationToken.None));
            AssertNoReadsOrWrites();
        }

        [TestCase("API Spec")]
        [TestCase("")]
        public void CompleteRun_RejectsWrongParentTypeBeforeMapping(string workItemType)
        {
            _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields["System.WorkItemType"] = workItemType);

            var error = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", SdkPullRequest, "draft", CancellationToken.None));

            Assert.That(error!.Message, Does.Contain("Expected work item type 'Release Plan'"));
            Assert.That(_workItems.Reads, Has.Count.EqualTo(1));
            AssertNoWrites();
        }

        [TestCase(null)]
        [TestCase(0)]
        [TestCase(-1)]
        public void CompleteRun_RejectsMissingRevisionBeforeMapping(int? revision)
        {
            _workItems.ChangeWorkItem(WorkItemId, parent => parent.Rev = revision);

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", SdkPullRequest, "draft", CancellationToken.None));

            Assert.That(_workItems.Reads, Has.Count.EqualTo(1));
            AssertNoWrites();
        }

        [TestCase("")]
        [TestCase("merged")]
        [TestCase("Completed")]
        [TestCase("ready")]
        public void CompleteRun_RejectsUnknownStatusBeforeReads(string status)
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", SdkPullRequest, status, CancellationToken.None));
            AssertNoReadsOrWrites();
        }

        [TestCase("draft")]
        [TestCase("ready for review")]
        public void CompleteRun_RequiresPullRequestForSuccessfulStatus(string status)
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", "", status, CancellationToken.None));
            AssertNoReadsOrWrites();
        }

        [TestCase("https://github.com/Azure/azure-sdk-for-net/pull/123")]
        [TestCase("https://github.com/Other/azure-sdk-for-java/pull/123")]
        [TestCase("http://github.com/Azure/azure-sdk-for-java/pull/123")]
        [TestCase("https://githubXcom/Azure/azure-sdk-for-java/pull/123")]
        [TestCase("https://github.com/Azure/azure-sdk-for-java/pull/0")]
        [TestCase("https://github.com/Azure/azure-sdk-for-java/pull/99999999999999999999999")]
        [TestCase("https://github.com/Azure/azure-sdk-for-java/pull/123/files")]
        [TestCase("PR: https://github.com/Azure/azure-sdk-for-java/pull/123")]
        public void CompleteRun_RejectsInvalidOrWrongLanguagePullRequestBeforeReads(string pullRequest)
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", pullRequest, "draft", CancellationToken.None));
            AssertNoReadsOrWrites();
        }

        [TestCase("sdk-review")]
        [TestCase("SDK-RELEASE")]
        [TestCase("")]
        [TestCase(null)]
        public void CompleteRun_OnlyExplicitReleaseTriggerMayReportReady(string? triggerSource)
        {
            if (triggerSource == null)
            {
                _build.TemplateParameters.Remove("TriggerSource");
            }
            else
            {
                _build.TemplateParameters["TriggerSource"] = triggerSource;
            }

            var error = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", SdkPullRequest, "ready for review", CancellationToken.None));

            Assert.That(error!.Message, Does.Contain("Expected TriggerSource 'sdk-release'"));
            AssertNoWrites();
        }

        [TestCase("beta", ApiVersion)]
        [TestCase("stable", "2026-01-01")]
        public async Task CompleteRun_ExplicitReleaseTriggerMayReportReady(string releaseType, string apiVersion)
        {
            _build.TemplateParameters["TriggerSource"] = "sdk-release";
            _build.TemplateParameters["SdkReleaseType"] = releaseType;
            _build.TemplateParameters["ApiVersion"] = apiVersion;
            _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields["Custom.SDKtypetobereleased"] = releaseType);
            _workItems.ChangeWorkItem(ApiSpecWorkItemId, child => child.Fields["Custom.APISpecversion"] = apiVersion);

            Assert.That(await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", SdkPullRequest, "ready for review", CancellationToken.None), Is.True);
            Assert.That(_workItems.GetStoredWorkItem(WorkItemId).Fields["Custom.SDKPullRequestStatusForJava"], Is.EqualTo("ready for review"));
        }

        [Test]
        public async Task CompleteRun_FailureWithoutPullRequest_PreservesExistingPullRequest()
        {
            Assert.That(await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", "", "Failed to generate SDK.", CancellationToken.None), Is.True);

            var updated = _workItems.GetStoredWorkItem(WorkItemId);
            Assert.That(updated.Fields["Custom.SDKPullRequestForJava"], Is.EqualTo(PreviousSdkPullRequest));
            Assert.That(updated.Fields["Custom.SDKPullRequestStatusForJava"], Is.EqualTo("Failed to generate SDK."));
            Assert.That(updated.Fields["Custom.GenerationStatusForJava"], Is.EqualTo("Completed"));
            var patch = _workItems.UpdateAttempts.Single().Patch;
            Assert.That(patch, Has.Count.EqualTo(5));
            Assert.That(patch.Any(operation => operation.Path == "/fields/Custom.SDKPullRequestForJava"), Is.False);
            Assert.That(patch.Any(operation => operation.Operation == DevOpsPatchOperation.Remove), Is.False);
        }

        [TestCase("Custom.ApiSpecProjectPath", "specification/other/Other.Management")]
        [TestCase("Custom.SDKtypetobereleased", "stable")]
        [TestCase("Custom.ReleaseStatusForJava", "Released")]
        [TestCase("Custom.SDKGenerationPipelineForJava", "another-run")]
        [TestCase("Custom.SpecCommitSHA", SpecCommit)]
        public void CompleteRun_ParentChangedWhileMapping_UsesFirstRevisionAndDoesNotRetry(string field, string value)
        {
            _workItems.BeforeRead = (id, count, parent) =>
            {
                if (id == WorkItemId && count == 2)
                {
                    parent.Rev++;
                    parent.Fields[field] = value;
                }
            };

            var error = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", SdkPullRequest, "draft", CancellationToken.None));

            Assert.That(error!.Message, Does.Contain("/rev"));
            Assert.That(_workItems.UpdateAttempts, Has.Count.EqualTo(1));
            Assert.That(_workItems.UpdateAttempts.Single().Patch[0].Value, Is.EqualTo(ParentRevision));
            Assert.That(_workItems.Reads.Count(read => read.Id == WorkItemId), Is.EqualTo(3), "No refresh-and-retry may replace the first revision.");
            Assert.That(_workItems.SuccessfulUpdates, Is.Zero);
            Assert.That(_workItems.GetStoredWorkItem(WorkItemId).Fields["Custom.SDKPullRequestForJava"], Is.EqualTo(PreviousSdkPullRequest));
        }

        [TestCase(OtherSpecCommit)]
        [TestCase("")]
        public void CompleteRun_PinChangedOrClearedWhileMapping_FailsBeforeWrite(string newPin)
        {
            _workItems.BeforeRead = (id, count, parent) =>
            {
                if (id == WorkItemId && count == 3)
                {
                    parent.Rev++;
                    parent.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = newPin;
                }
            };

            var error = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", SdkPullRequest, "draft", CancellationToken.None));

            Assert.That(error!.Message, Does.Contain("SpecCommitSHA"));
            AssertNoWrites();
        }

        [TestCase("/rev")]
        [TestCase("/fields/Custom.SpecCommitSHA")]
        [TestCase("/fields/Custom.SDKGenerationPipelineForJava")]
        public void CompleteRun_EachConditionalGuardRejectsConcurrentChange(string path)
        {
            _workItems.BeforeUpdate = parent =>
            {
                if (path == "/rev")
                {
                    parent.Rev++;
                }
                else
                {
                    parent.Fields[path["/fields/".Length..]] = "changed";
                }
            };

            var error = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", SdkPullRequest, "draft", CancellationToken.None));

            Assert.That(error!.Message, Does.Contain(path));
            Assert.That(_workItems.UpdateAttempts, Has.Count.EqualTo(1));
            Assert.That(_workItems.SuccessfulUpdates, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BuildErrors_PropagateWithoutWorkItemReads(bool complete)
        {
            var failure = new HttpRequestException("Build read failed.");
            _buildClient.Setup(client => client.GetBuildAsync("internal", BuildId, null, null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(failure);

            Assert.That(Assert.ThrowsAsync<HttpRequestException>(() => RunAsync(complete, CancellationToken.None)), Is.SameAs(failure));
            Assert.That(_workItems.Reads, Is.Empty);
            AssertNoWrites();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ParentReadErrors_PropagateWithoutWrites(bool complete)
        {
            var failure = new HttpRequestException("Parent read failed.");
            _workItems.ReadFailure = failure;
            _workItems.ReadFailureId = WorkItemId;

            Assert.That(Assert.ThrowsAsync<HttpRequestException>(() => RunAsync(complete, CancellationToken.None)), Is.SameAs(failure));
            AssertNoWrites();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ChildReadFailure_FailsClosedDespiteExistingMapperRecovery(bool complete)
        {
            _workItems.ReadFailure = new HttpRequestException("Child read failed.");
            _workItems.ReadFailureId = ApiSpecWorkItemId;

            var error = Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(complete, CancellationToken.None));

            Assert.That(error!.Message, Does.Contain("SpecCommitSHA"));
            AssertNoWrites();
        }

        [Test]
        public void CompleteRun_UpdateConflict_PropagatesWithoutRetry()
        {
            var conflict = new InvalidOperationException("409: revision conflict");
            _workItems.UpdateFailure = conflict;

            Assert.That(Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(true, CancellationToken.None)), Is.SameAs(conflict));
            Assert.That(_workItems.UpdateAttempts, Has.Count.EqualTo(1));
            Assert.That(_workItems.SuccessfulUpdates, Is.Zero);
            _buildClient.Verify(client => client.GetBuildAsync("internal", BuildId, null, null, CancellationToken.None), Times.Once);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AlreadyCancelled_PropagatesBeforeReads(bool complete)
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(() => RunAsync(complete, cancellation.Token));
            AssertNoReadsOrWrites();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BuildCancellation_PropagatesWithoutWorkItemReads(bool complete)
        {
            using var cancellation = new CancellationTokenSource();
            var failure = new OperationCanceledException(cancellation.Token);
            _buildClient.Setup(client => client.GetBuildAsync("internal", BuildId, null, null, cancellation.Token)).ThrowsAsync(failure);

            Assert.That(Assert.CatchAsync<OperationCanceledException>(() => RunAsync(complete, cancellation.Token)), Is.SameAs(failure));
            Assert.That(_workItems.Reads, Is.Empty);
            AssertNoWrites();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ChildReadCancellation_PropagatesWithoutWrites(bool complete)
        {
            using var cancellation = new CancellationTokenSource();
            _workItems.BeforeRead = (id, _, _) =>
            {
                if (id == ApiSpecWorkItemId)
                {
                    cancellation.Cancel();
                }
            };

            var failure = Assert.CatchAsync<OperationCanceledException>(() => RunAsync(complete, cancellation.Token));

            Assert.That(failure!.CancellationToken, Is.EqualTo(cancellation.Token));
            Assert.That(_workItems.Reads.All(read => read.Token == cancellation.Token), Is.True);
            AssertNoWrites();
        }

        [Test]
        public void CompleteRun_UpdateCancellation_PropagatesWithoutRetry()
        {
            using var cancellation = new CancellationTokenSource();
            _workItems.BeforeUpdate = _ => cancellation.Cancel();

            var failure = Assert.CatchAsync<OperationCanceledException>(() => RunAsync(true, cancellation.Token));

            Assert.That(failure!.CancellationToken, Is.EqualTo(cancellation.Token));
            Assert.That(_workItems.UpdateAttempts, Has.Count.EqualTo(1));
            Assert.That(_workItems.UpdateAttempts.Single().Token, Is.EqualTo(cancellation.Token));
            Assert.That(_workItems.SuccessfulUpdates, Is.Zero);
        }

        private async Task RunAsync(bool complete, CancellationToken ct)
        {
            if (complete)
            {
                await _service.CompleteSdkGenerationAsync(WorkItemId, BuildId, "Java", SdkPullRequest, "draft", ct);
            }
            else
            {
                await _service.ValidateSdkGenerationRunAsync(WorkItemId, BuildId, "Java", ct);
            }
        }

        private void AssertNoWrites()
        {
            Assert.That(_workItems.UpdateAttempts, Is.Empty);
            Assert.That(_workItems.SuccessfulUpdates, Is.Zero);
        }

        private void AssertNoReadsOrWrites()
        {
            _connection.Verify(connection => connection.GetBuildClient(It.IsAny<CancellationToken>()), Times.Never);
            Assert.That(_workItems.Reads, Is.Empty);
            AssertNoWrites();
        }

        private static WorkItem CreateParent()
        {
            var parent = new WorkItem
            {
                Id = WorkItemId,
                Rev = ParentRevision,
                Fields = new Dictionary<string, object>
                {
                    ["System.WorkItemType"] = "Release Plan",
                    ["System.State"] = "In Progress",
                    [ReleasePlanWorkItem.SpecCommitSHAField] = SpecCommit,
                    ["Custom.SDKtypetobereleased"] = "beta",
                    ["Custom.ApiSpecProjectPath"] = ProjectPath,
                    ["Custom.ReleasePlanType"] = "APEX Public Preview"
                },
                Relations =
                [
                    new WorkItemRelation
                    {
                        Rel = "System.LinkTypes.Hierarchy-Forward",
                        Url = $"https://dev.azure.com/test/_apis/wit/workItems/{ApiSpecWorkItemId}"
                    }
                ]
            };
            foreach (var language in new[] { "Dotnet", "JavaScript", "Python", "Java", "Go" })
            {
                parent.Fields[$"Custom.SDKGenerationPipelineFor{language}"] = DevOpsService.GetPipelineUrl(BuildId);
                parent.Fields[$"Custom.GenerationStatusFor{language}"] = "In progress";
                parent.Fields[$"Custom.ReleaseStatusFor{language}"] = "Not released";
            }
            parent.Fields["Custom.SDKPullRequestForJava"] = PreviousSdkPullRequest;
            return parent;
        }

        private void ChangeSnapshot(string scenario)
        {
            switch (scenario)
            {
                case "build-id": _build.Id++; break;
                case "definition-id": _build.Definition.Id = 7423; break;
                case "missing-definition": _build.Definition = null; break;
                case "source-sha": _build.SourceVersion = OtherSpecCommit; break;
                case "invalid-source-sha": _build.SourceVersion = "abcdef"; break;
                case "missing-source-sha":
                    _build.SourceVersion = null;
                    _build.SourceBranch = SpecCommit;
                    _build.TemplateParameters["SpecCommitSha"] = SpecCommit;
                    break;
                case "template-api-version": _build.TemplateParameters["ApiVersion"] = "2026-02-01"; break;
                case "template-release-type": _build.TemplateParameters["SdkReleaseType"] = "stable"; break;
                case "template-project": _build.TemplateParameters["ConfigPath"] = "specification/other/tspconfig.yaml"; break;
                case "template-project-case": _build.TemplateParameters["ConfigPath"] = $"{ProjectPath.ToUpperInvariant()}/tspconfig.yaml"; break;
                case "template-config-type": _build.TemplateParameters["ConfigType"] = "Swagger"; break;
                case "template-work-item-id": _build.TemplateParameters["ReleasePlanWorkItemId"] = "12345"; break;
                case "missing-template-parameters": _build.TemplateParameters = null; break;
                case "stored-sha": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = OtherSpecCommit); break;
                case "missing-stored-sha": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields.Remove(ReleasePlanWorkItem.SpecCommitSHAField)); break;
                case "invalid-stored-sha": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = "main"); break;
                case "transition-stored-sha": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = ""); break;
                case "stored-api-version": _workItems.ChangeWorkItem(ApiSpecWorkItemId, child => child.Fields["Custom.APISpecversion"] = "2026-02-01"); break;
                case "missing-stored-api-version": _workItems.ChangeWorkItem(ApiSpecWorkItemId, child => child.Fields.Remove("Custom.APISpecversion")); break;
                case "none-stored-api-version": _workItems.ChangeWorkItem(ApiSpecWorkItemId, child => child.Fields["Custom.APISpecversion"] = "none"); break;
                case "stored-release-type": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields["Custom.SDKtypetobereleased"] = "stable"); break;
                case "missing-stored-release-type": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields.Remove("Custom.SDKtypetobereleased")); break;
                case "stored-project": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields["Custom.ApiSpecProjectPath"] = "specification/other"); break;
                case "missing-stored-project": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields.Remove("Custom.ApiSpecProjectPath")); break;
                case "latest-url": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields["Custom.SDKGenerationPipelineForJava"] = DevOpsService.GetPipelineUrl(BuildId + 1)); break;
                case "missing-latest-url": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields.Remove("Custom.SDKGenerationPipelineForJava")); break;
                case "foreign-latest-url": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields["Custom.SDKGenerationPipelineForJava"] = $"https://dev.azure.com/other/internal/_build/results?buildId={BuildId}"); break;
                case "released-sdk": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields["Custom.ReleaseStatusForJava"] = "rElEaSeD"); break;
                case "private-plan": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Fields["Custom.ReleasePlanType"] = "apex private preview"); break;
                case "missing-child": _workItems.ChangeWorkItem(WorkItemId, parent => parent.Relations.Clear()); break;
                default:
                    if (!scenario.StartsWith("missing-", StringComparison.Ordinal) || !_build.TemplateParameters.Remove(scenario["missing-".Length..]))
                    {
                        throw new ArgumentException($"Unknown snapshot mutation '{scenario}'.", nameof(scenario));
                    }
                    break;
            }
        }
    }
}