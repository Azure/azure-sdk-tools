// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using DevOpsJsonPatchDocument = Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    [TestFixture]
    public class DevOpsServiceTests
    {
        private TestDevOpsConnection _connection = null!;
        private TestLogger<DevOpsService> _logger = null!;
        private DevOpsService _devOpsService = null!;

        [SetUp]
        public void SetUp()
        {
            _connection = new TestDevOpsConnection();
            _logger = new TestLogger<DevOpsService>();
            _devOpsService = new DevOpsService(_logger, _connection);
        }

        [TestCase("January 2020")]
        [TestCase("Jan 2020")]
        public async Task ListOverdueReleasePlansAsync_PrivatePreviewWithoutSpecChild_IsMissing(string targetMonth)
        {
            var plan = CreateReleasePlanWorkItem(100, "In Progress");
            plan.Fields["Custom.ReleasePlanType"] = ApiReleaseType.PrivatePreview.ToAdoFieldValue();
            plan.Fields["Custom.SDKReleasemonth"] = targetMonth;
            _connection.AddWorkItemToQuery(plan);

            var result = await _devOpsService.ListOverdueReleasePlansAsync(CancellationToken.None);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].ApiReleaseType, Is.EqualTo(ApiReleaseType.PrivatePreview));
            Assert.That(result[0].ActiveSpecPullRequest, Is.Empty);
        }

        [Test]
        public void ListOverdueReleasePlansAsync_UnreadablePrivateSpecChild_DoesNotBecomeMissing()
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Fields["Custom.ReleasePlanType"] = ApiReleaseType.PrivatePreview.ToAdoFieldValue();
            plan.Fields["Custom.SDKReleasemonth"] = "January 2020";
            _connection.AddWorkItemToQuery(plan);
            _connection.AddWorkItem(plan);

            var error = Assert.ThrowsAsync<Exception>(async () =>
                await _devOpsService.ListOverdueReleasePlansAsync(CancellationToken.None));

            Assert.That(error!.GetBaseException().Message, Does.Contain("200"));
            Assert.That(error.Message, Does.Contain("Work item 200 not found"));
            Assert.That(error.Message, Does.Not.Contain("{ex}"));
        }

        [Test]
        public async Task ListOverdueReleasePlansAsync_MapsPrivateSpecPullRequest()
        {
            const string specPr = "https://github.com/Azure/azure-rest-api-specs-pr/pull/42";
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Rev = 7;
            plan.Fields["Custom.ReleasePlanType"] = ApiReleaseType.PrivatePreview.ToAdoFieldValue();
            plan.Fields["Custom.SDKReleasemonth"] = "January 2020";
            _connection.AddWorkItemToQuery(plan);
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(CreateApiSpecWorkItem(200, specPr, "New"));

            var result = await _devOpsService.ListOverdueReleasePlansAsync(CancellationToken.None);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].ActiveSpecPullRequest, Is.EqualTo(specPr));
            Assert.That(result[0].Revision, Is.EqualTo(7));
        }

        [Test]
        public async Task UpdateWorkItemAsync_WithExpectedRevision_TestsRevisionBeforeUpdatingState()
        {
            var plan = CreateReleasePlanWorkItem(100, "In Progress");
            plan.Rev = 7;
            _connection.AddWorkItem(plan);

            var result = await _devOpsService.UpdateWorkItemAsync(100,
                new Dictionary<string, string> { ["System.State"] = "Abandoned" }, 7, CancellationToken.None);

            var patch = _connection.LastCapturedPatchDocument!;
            Assert.That(patch, Has.Count.EqualTo(2));
            Assert.That(patch[0].Operation, Is.EqualTo(Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Test));
            Assert.That(patch[0].Path, Is.EqualTo("/rev"));
            Assert.That(patch[0].Value, Is.EqualTo(7));
            Assert.That(patch[1].Path, Is.EqualTo("/fields/System.State"));
            Assert.That(patch[1].Value, Is.EqualTo("Abandoned"));
            Assert.That(result.Fields["System.State"], Is.EqualTo("Abandoned"));
        }

        [TestCase("Custom.SDKReleasemonth", "December 2026")]
        [TestCase("Custom.ReleaseStatusForPython", "Released")]
        [TestCase("System.State", "Finished")]
        public void UpdateWorkItemAsync_ChangedRevision_RejectsAbandonmentWithoutRetry(string changedField, string value)
        {
            var plan = CreateReleasePlanWorkItem(100, "In Progress");
            plan.Fields[changedField] = value;
            plan.Rev = 8; // The owner or release automation changed the plan after revision 7 was scanned.
            _connection.AddWorkItem(plan);

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _devOpsService.UpdateWorkItemAsync(100,
                    new Dictionary<string, string> { ["System.State"] = "Abandoned" }, 7, CancellationToken.None));

            Assert.That(plan.Fields[changedField], Is.EqualTo(value));
            Assert.That(plan.Fields["System.State"], Is.Not.EqualTo("Abandoned"));
            Assert.That(_connection.WorkItemUpdateCount, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void UpdateWorkItemAsync_InvalidExpectedRevision_DoesNotSendPatch(int revision)
        {
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await _devOpsService.UpdateWorkItemAsync(100,
                    new Dictionary<string, string> { ["System.State"] = "Abandoned" }, revision, CancellationToken.None));

            Assert.That(_connection.LastCapturedPatchDocument, Is.Null);
                }

        [Test]
        public async Task EnsureReleasePlanAutomationRelation_AddsRelatedLinkWithContextAndPreservesOtherLinks()
        {
            var previous = new WorkItem { Id = 100, Url = "https://dev.azure.com/test/_apis/wit/workItems/100" };
            var pending = new WorkItem
            {
                Id = 200,
                Relations = [new WorkItemRelation { Rel = "System.LinkTypes.Related", Url = "https://dev.azure.com/test/_apis/wit/workItems/300" }]
            };
            _connection.AddWorkItem(previous);
            _connection.AddWorkItem(pending);

            await _devOpsService.EnsureReleasePlanAutomationRelationAsync(200, 100, CancellationToken.None);
            await _devOpsService.EnsureReleasePlanAutomationRelationAsync(200, 100, CancellationToken.None);

            Assert.That(_connection.CapturedPatches, Has.Count.EqualTo(1));
            Assert.That(_connection.CapturedPatches[0].WorkItemId, Is.EqualTo(200));
            var patch = _connection.CapturedPatches[0].Document.Single();
            Assert.That(patch.Path, Is.EqualTo("/relations/-"));
            var relation = (WorkItemRelation)patch.Value;
            Assert.That(relation.Rel, Is.EqualTo("System.LinkTypes.Related"));
            Assert.That(relation.Url, Is.EqualTo(previous.Url));
            Assert.That(relation.Attributes["comment"].ToString(), Does.Contain("Automatic SDK generation"));
            Assert.That(pending.Relations, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task EnsureReleasePlanAutomationRelation_ConcurrentInsert_IsSuccessful()
        {
            _connection.AddWorkItem(new WorkItem { Id = 100, Url = "https://dev.azure.com/test/_apis/wit/workItems/100" });
            _connection.AddWorkItem(new WorkItem { Id = 200, Relations = [] });
            _connection.FailNextRelationUpdate(addRelationBeforeFailure: true);

            await _devOpsService.EnsureReleasePlanAutomationRelationAsync(200, 100, CancellationToken.None);

            Assert.That(_connection.CapturedPatches, Has.Count.EqualTo(1));
        }

        [Test]
        public void EnsureReleasePlanAutomationRelation_UpdateFailureWithoutLink_Propagates()
        {
            _connection.AddWorkItem(new WorkItem { Id = 100, Url = "https://dev.azure.com/test/_apis/wit/workItems/100" });
            _connection.AddWorkItem(new WorkItem { Id = 200, Relations = [] });
            _connection.FailNextRelationUpdate(addRelationBeforeFailure: false);

            Assert.ThrowsAsync<VssServiceException>(() =>
                _devOpsService.EnsureReleasePlanAutomationRelationAsync(200, 100, CancellationToken.None));
        }

        [Test]
        public void EnsureReleasePlanAutomationRelation_Cancellation_DoesNotUpdate()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.ThrowsAsync<TaskCanceledException>(() =>
                _devOpsService.EnsureReleasePlanAutomationRelationAsync(200, 100, cts.Token));
            Assert.That(_connection.CapturedPatches, Is.Empty);
        }

        #region GetReleasePlanAsync(string pullRequestUrl) Tests

        [Test]
        public async Task GetReleasePlanAsync_WithPullRequestUrl_ShouldSkipAbandonedParent()
        {
            // Arrange
            var pullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/12345";
            var apiSpecWorkItem = CreateApiSpecWorkItem(1, pullRequestUrl, "Active");
            var abandonedParent = CreateReleasePlanWorkItem(100, "Abandoned");

            _connection.AddWorkItemToQuery(apiSpecWorkItem);
            _connection.AddWorkItem(abandonedParent);

            // Act
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl, ct: CancellationToken.None);

            // Assert
            Assert.IsNull(result, "Should return null when parent release plan is in Abandoned state");
        }

        [Test]
        public async Task GetReleasePlanAsync_WithPullRequestUrl_ShouldSkipClosedParent()
        {
            // Arrange
            var pullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/12345";
            var apiSpecWorkItem = CreateApiSpecWorkItem(1, pullRequestUrl, "Active");
            var closedParent = CreateReleasePlanWorkItem(100, "Closed");

            _connection.AddWorkItemToQuery(apiSpecWorkItem);
            _connection.AddWorkItem(closedParent);

            // Act
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl, ct: CancellationToken.None);

            // Assert
            Assert.IsNull(result, "Should return null when parent release plan is in Closed state");
        }

        [Test]
        public async Task GetReleasePlanAsync_WithPullRequestUrl_ShouldSkipDuplicateParent()
        {
            // Arrange
            var pullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/12345";
            var apiSpecWorkItem = CreateApiSpecWorkItem(1, pullRequestUrl, "Active");
            var duplicateParent = CreateReleasePlanWorkItem(100, "Duplicate");

            _connection.AddWorkItemToQuery(apiSpecWorkItem);
            _connection.AddWorkItem(duplicateParent);

            // Act
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl, ct: CancellationToken.None);

            // Assert
            Assert.IsNull(result, "Should return null when parent release plan is in Duplicate state");
        }

        [Test]
        public async Task GetReleasePlanAsync_WithPullRequestUrl_ShouldReturnActiveParent()
        {
            // Arrange
            var pullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/12345";
            var apiSpecWorkItem = CreateApiSpecWorkItem(1, pullRequestUrl, "Active");
            var activeParent = CreateReleasePlanWorkItem(100, "In Progress");

            _connection.AddWorkItemToQuery(apiSpecWorkItem);
            _connection.AddWorkItem(activeParent);

            // Act
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl, ct: CancellationToken.None);

            // Assert
            Assert.IsNotNull(result, "Should return release plan when parent is in valid state");
            Assert.That(result.WorkItemId, Is.EqualTo(100));
        }

        [Test]
        public async Task GetReleasePlanAsync_WithPullRequestUrl_ShouldReturnNewParent()
        {
            // Arrange
            var pullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/12345";
            var apiSpecWorkItem = CreateApiSpecWorkItem(1, pullRequestUrl, "Active");
            var newParent = CreateReleasePlanWorkItem(100, "New");

            _connection.AddWorkItemToQuery(apiSpecWorkItem);
            _connection.AddWorkItem(newParent);

            // Act
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl, ct: CancellationToken.None);

            // Assert
            Assert.IsNotNull(result, "Should return release plan when parent is in New state");
            Assert.That(result.WorkItemId, Is.EqualTo(100));
        }

        [Test]
        public async Task GetReleasePlanAsync_WithPullRequestUrl_MultipleParents_ShouldSkipAbandonedAndReturnActive()
        {
            // Arrange
            var pullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/12345";
            var apiSpecWorkItem1 = CreateApiSpecWorkItem(1, pullRequestUrl, "Active", parentId: 100);
            var apiSpecWorkItem2 = CreateApiSpecWorkItem(2, pullRequestUrl, "Active", parentId: 200);
            var abandonedParent = CreateReleasePlanWorkItem(100, "Abandoned");
            var activeParent = CreateReleasePlanWorkItem(200, "In Progress");

            _connection.AddWorkItemToQuery(apiSpecWorkItem1);
            _connection.AddWorkItemToQuery(apiSpecWorkItem2);
            _connection.AddWorkItem(abandonedParent);
            _connection.AddWorkItem(activeParent);

            // Act
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl, ct: CancellationToken.None);

            // Assert
            Assert.IsNotNull(result, "Should return active release plan when one parent is abandoned and another is active");
            Assert.That(result.WorkItemId, Is.EqualTo(200));
        }

        [Test]
        public async Task GetReleasePlanAsync_WithPullRequestUrl_CaseInsensitiveStateCheck()
        {
            // Arrange
            var pullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/12345";
            var apiSpecWorkItem = CreateApiSpecWorkItem(1, pullRequestUrl, "Active");
            var abandonedParent = CreateReleasePlanWorkItem(100, "ABANDONED"); // uppercase

            _connection.AddWorkItemToQuery(apiSpecWorkItem);
            _connection.AddWorkItem(abandonedParent);

            // Act
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl, ct: CancellationToken.None);

            // Assert
            Assert.IsNull(result, "Should handle state comparison case-insensitively");
        }

        [Test]
        public async Task GetReleasePlanAsync_WithPullRequestUrl_NullRelations_ShouldNotThrow()
        {
            // Arrange: the ADO client leaves Relations null (rather than empty) for work items
            // returned without any relations, e.g. an API Spec item never linked to a parent.
            var pullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/12345";
            var apiSpecWorkItem = CreateApiSpecWorkItem(1, pullRequestUrl, "Active");
            apiSpecWorkItem.Relations = null;

            _connection.AddWorkItemToQuery(apiSpecWorkItem);

            // Act
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl, ct: CancellationToken.None);

            // Assert
            Assert.IsNull(result, "Should return null instead of throwing when Relations is null");
        }

        [Test]
        public async Task GetReleasePlanAsync_WithPullRequestUrl_DifferentConcreteReleaseTypeStillSkipped()
        {
            // Arrange: an existing GA plan; requesting Public Preview must NOT treat it as a duplicate,
            // since multiple release types are allowed to coexist for the same spec PR/TypeSpec project.
            var pullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/12345";
            var apiSpecWorkItem = CreateApiSpecWorkItem(1, pullRequestUrl, "Active");
            var gaParent = CreateReleasePlanWorkItem(100, "In Progress");
            gaParent.Fields["Custom.ReleasePlanType"] = "GA";

            _connection.AddWorkItemToQuery(apiSpecWorkItem);
            _connection.AddWorkItem(gaParent);

            // Act
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl, ApiReleaseType.PublicPreview, CancellationToken.None);

            // Assert: a different, concrete release type must not be treated as a duplicate.
            Assert.IsNull(result, "A plan with a different, concrete release type must not be treated as a duplicate.");
        }

        #endregion

        #region ResolveReleasePlanByIdAsync Tests

        [Test]
        public async Task ResolveReleasePlanByIdAsync_WithWorkItemId_FallsBackAndResolves()
        {
            // Arrange: a Release Plan whose work item ID (35000) differs from its Release Plan ID (50001).
            // It is only registered as a work item (not discoverable via the Release Plan ID query),
            // so resolution must fall back to the work item ID lookup.
            var plan = CreateReleasePlanWorkItemWithReleasePlanId(workItemId: 35000, releasePlanId: 50001, state: "In Progress");
            _connection.AddWorkItem(plan);

            // Act: caller passes the work item ID (the rare edge case).
            var result = await _devOpsService.ResolveReleasePlanByIdAsync(35000, CancellationToken.None);

            // Assert: the Release Plan ID lookup fails, then the work item ID fallback resolves it.
            Assert.IsNotNull(result, "Should fall back and resolve when given the work item ID.");
            Assert.That(result!.WorkItemId, Is.EqualTo(35000));
            Assert.That(result.ReleasePlanId, Is.EqualTo(50001));
        }

        [Test]
        public async Task ResolveReleasePlanByIdAsync_WithReleasePlanId_ResolvesViaPrimaryLookup()
        {
            // Arrange: the plan is discoverable via the Release Plan ID query (50001), which is the
            // primary lookup. This is the common case: users have the Release Plan ID in hand.
            var plan = CreateReleasePlanWorkItemWithReleasePlanId(workItemId: 35000, releasePlanId: 50001, state: "In Progress");
            _connection.AddWorkItemToQuery(plan);

            // Act: caller passes the user-facing Release Plan ID.
            var result = await _devOpsService.ResolveReleasePlanByIdAsync(50001, CancellationToken.None);

            // Assert: the Release Plan ID lookup resolves to the right plan.
            Assert.IsNotNull(result, "Should resolve via the Release Plan ID lookup.");
            Assert.That(result!.WorkItemId, Is.EqualTo(35000));
            Assert.That(result.ReleasePlanId, Is.EqualTo(50001));
        }

        [Test]
        public async Task ResolveReleasePlanByIdAsync_WhenWorkItemIsNotReleasePlan_DoesNotMisresolve()
        {
            // Arrange: a work item with the given id exists but is NOT a Release Plan, and there is no
            // Release Plan with that Release Plan ID either.
            var apiSpecWorkItem = CreateApiSpecWorkItem(35000, "https://github.com/Azure/azure-rest-api-specs/pull/1", "Active");
            _connection.AddWorkItem(apiSpecWorkItem);

            // Act
            var result = await _devOpsService.ResolveReleasePlanByIdAsync(35000, CancellationToken.None);

            // Assert: it must not map a non-Release-Plan work item, and falls back to null.
            Assert.IsNull(result, "Should not resolve a non-Release-Plan work item.");
        }

        [Test]
        public async Task ResolveReleasePlanByIdAsync_WithInvalidId_ReturnsNull()
        {
            Assert.IsNull(await _devOpsService.ResolveReleasePlanByIdAsync(0, CancellationToken.None));
            Assert.IsNull(await _devOpsService.ResolveReleasePlanByIdAsync(-5, CancellationToken.None));
        }

        #endregion

        #region GetReleasePlanForWorkItemAsync Tests

        [Test]
        public async Task GetReleasePlanForWorkItemAsync_WhenWorkItemIsReleasePlan_Maps()
        {
            // Arrange
            var plan = CreateReleasePlanWorkItem(35000, "In Progress");
            _connection.AddWorkItem(plan);

            // Act
            var result = await _devOpsService.GetReleasePlanForWorkItemAsync(35000, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.That(result.WorkItemId, Is.EqualTo(35000));
        }

        [Test]
        public void GetReleasePlanForWorkItemAsync_WhenWorkItemIsNotReleasePlan_Throws()
        {
            // Arrange: the work item exists but is an API Spec, not a Release Plan.
            var apiSpecWorkItem = CreateApiSpecWorkItem(35000, "https://github.com/Azure/azure-rest-api-specs/pull/1", "Active");
            _connection.AddWorkItem(apiSpecWorkItem);

            // Act + Assert: must not map a non-Release-Plan work item to a release plan.
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _devOpsService.GetReleasePlanForWorkItemAsync(35000, CancellationToken.None));
            Assert.That(ex!.Message, Does.Contain("is not a Release Plan"));
        }

        #endregion

        #region UpdateReleasePlanSDKDetailsAsync Tests

        [Test]
        public async Task UpdateReleasePlanSDKDetailsAsync_WhenCurrentStatusIsMissingEmitterConfig_ResetsExclusionStatusToNotApplicable()
        {
            // Arrange: the language was previously auto-marked MissingEmitterConfig because the parser did
            // not detect a package name. Now a package name is detected, so the status must be reset.
            var plan = CreateReleasePlanWorkItemWithExclusionStatus(35000, "Python", "MissingEmitterConfig");
            _connection.AddWorkItem(plan);
            var sdkLanguages = new List<SDKInfo>
            {
                new() { Language = "Python", PackageName = "azure-mgmt-contoso" }
            };

            // Act
            var result = await _devOpsService.UpdateReleasePlanSDKDetailsAsync(35000, sdkLanguages, CancellationToken.None);

            // Assert
            Assert.That(result, Is.True);
            var patch = _connection.LastCapturedPatchDocument;
            Assert.IsNotNull(patch, "UpdateWorkItemAsync should have been called with a patch document");
            var resetOp = patch!.FirstOrDefault(op => op.Path == "/fields/Custom.ReleaseExclusionStatusForPython");
            Assert.IsNotNull(resetOp, "Exclusion status should be reset when the current status is MissingEmitterConfig");
            Assert.That(resetOp!.Value, Is.EqualTo("Not applicable"));
        }

        [Test]
        public async Task UpdateReleasePlanSDKDetailsAsync_WhenCurrentStatusIsRequested_DoesNotResetExclusionStatus()
        {
            // Arrange: an intentional exclusion (Requested) must be preserved even when a package name is detected.
            var plan = CreateReleasePlanWorkItemWithExclusionStatus(35000, "Java", "Requested");
            _connection.AddWorkItem(plan);
            var sdkLanguages = new List<SDKInfo>
            {
                new() { Language = "Java", PackageName = "com.azure.contoso" }
            };

            // Act
            var result = await _devOpsService.UpdateReleasePlanSDKDetailsAsync(35000, sdkLanguages, CancellationToken.None);

            // Assert
            Assert.That(result, Is.True);
            var patch = _connection.LastCapturedPatchDocument;
            Assert.IsNotNull(patch);
            // The package name is still updated, but an intentional exclusion must not be reset.
            Assert.That(patch!.Any(op => op.Path == "/fields/Custom.JavaPackageName"), Is.True);
            Assert.That(patch.Any(op => op.Path == "/fields/Custom.ReleaseExclusionStatusForJava"), Is.False,
                "Requested exclusion status must not be reset");
        }

        [Test]
        public async Task UpdateReleasePlanSDKDetailsAsync_WhenCurrentStatusIsApproved_DoesNotResetExclusionStatus()
        {
            // Arrange: an approved exclusion must be preserved.
            var plan = CreateReleasePlanWorkItemWithExclusionStatus(35000, "Go", "Approved");
            _connection.AddWorkItem(plan);
            var sdkLanguages = new List<SDKInfo>
            {
                new() { Language = "Go", PackageName = "sdk/contoso/armcontoso" }
            };

            // Act
            var result = await _devOpsService.UpdateReleasePlanSDKDetailsAsync(35000, sdkLanguages, CancellationToken.None);

            // Assert
            Assert.That(result, Is.True);
            var patch = _connection.LastCapturedPatchDocument;
            Assert.IsNotNull(patch);
            Assert.That(patch!.Any(op => op.Path == "/fields/Custom.ReleaseExclusionStatusForGo"), Is.False,
                "Approved exclusion status must not be reset");
        }

        [Test]
        public async Task UpdateReleasePlanSDKDetailsAsync_WhenCurrentStatusIsEmpty_DoesNotResetExclusionStatus()
        {
            // Arrange: no prior exclusion status, so there is nothing to reset.
            var plan = CreateReleasePlanWorkItem(35000, "In Progress");
            _connection.AddWorkItem(plan);
            var sdkLanguages = new List<SDKInfo>
            {
                new() { Language = "Python", PackageName = "azure-mgmt-contoso" }
            };

            // Act
            var result = await _devOpsService.UpdateReleasePlanSDKDetailsAsync(35000, sdkLanguages, CancellationToken.None);

            // Assert
            Assert.That(result, Is.True);
            var patch = _connection.LastCapturedPatchDocument;
            Assert.IsNotNull(patch);
            Assert.That(patch!.Any(op => op.Path == "/fields/Custom.ReleaseExclusionStatusForPython"), Is.False,
                "An empty exclusion status must not be reset");
        }

        [Test]
        public async Task UpdateReleasePlanSDKDetailsAsync_MissingEmitterConfigComparisonIsCaseInsensitive()
        {
            // Arrange: the current status comparison must be case-insensitive.
            var plan = CreateReleasePlanWorkItemWithExclusionStatus(35000, "Python", "missingemitterconfig");
            _connection.AddWorkItem(plan);
            var sdkLanguages = new List<SDKInfo>
            {
                new() { Language = "Python", PackageName = "azure-mgmt-contoso" }
            };

            // Act
            var result = await _devOpsService.UpdateReleasePlanSDKDetailsAsync(35000, sdkLanguages, CancellationToken.None);

            // Assert
            Assert.That(result, Is.True);
            var patch = _connection.LastCapturedPatchDocument;
            Assert.IsNotNull(patch);
            var resetOp = patch!.FirstOrDefault(op => op.Path == "/fields/Custom.ReleaseExclusionStatusForPython");
            Assert.IsNotNull(resetOp, "MissingEmitterConfig comparison must be case-insensitive");
            Assert.That(resetOp!.Value, Is.EqualTo("Not applicable"));
        }

        #endregion

        #region UpdateSpecPullRequestAsync Tests

        [TestCase("Not applicable", "")]
        [TestCase("Pending", "")]
        [TestCase("Failed", "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=90")]
        [TestCase("Completed", "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=90")]
        [TestCase("In progress", "")]
        [TestCase("In progress", " \t")]
        public async Task UpdateSpecPullRequestAsync_NewSpec_MarksWaitingLanguagesNotApplicable(string generationStatus, string pipelineUrl)
        {
            const string oldSpec = "https://github.com/Azure/azure-rest-api-specs/pull/123";
            const string newSpec = "https://github.com/Azure/azure-rest-api-specs/pull/456";
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            var apiSpec = CreateApiSpecWorkItem(200, oldSpec, "Active");
            apiSpec.Fields["Custom.RESTAPIReviews"] = $"<a href=\"{oldSpec}\">{oldSpec}</a>";
            string[] languages = ["Dotnet", "Java", "JavaScript", "Go", "Python"];
            foreach (var language in languages)
            {
                plan.Fields[$"Custom.GenerationStatusFor{language}"] = generationStatus;
                plan.Fields[$"Custom.SDKGenerationPipelineFor{language}"] = pipelineUrl;
                plan.Fields[$"Custom.SDKPullRequestFor{language}"] = "existing-sdk-pr";
            }
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(apiSpec);

            var result = await _devOpsService.UpdateSpecPullRequestAsync(100, new ReleasePlanSpecTarget { SpecPullRequestUrl = newSpec }, [], [], CancellationToken.None);

            Assert.That(result, Is.True);
            Assert.That(_connection.CapturedPatches, Has.Count.EqualTo(3));
            var specPatch = _connection.CapturedPatches.Single(p => p.WorkItemId == 200).Document;
            Assert.That(specPatch.Single(op => op.Path == "/fields/Custom.ActiveSpecPullRequestUrl").Value, Is.EqualTo(newSpec));
            Assert.That(specPatch.Single(op => op.Path == "/fields/Custom.RESTAPIReviews").Value,
                Is.EqualTo($"<a href=\"{oldSpec}\">{oldSpec}</a><br><a href=\"{newSpec}\">{newSpec}</a>"));
            var statusPatch = _connection.CapturedPatches.Last().Document;
            Assert.That(statusPatch, Has.Count.EqualTo(languages.Length + 2));
            foreach (var language in languages)
            {
                Assert.That(statusPatch.Single(op => op.Path == $"/fields/Custom.GenerationStatusFor{language}").Value, Is.EqualTo("Not applicable"));
            }
            Assert.That(statusPatch.Skip(2).All(op => op.Path.StartsWith("/fields/Custom.GenerationStatusFor", StringComparison.Ordinal)), Is.True,
                "Linking a spec must not remove pipeline or SDK PR links, or change release/exclusion state.");
        }

        [TestCase("In progress")]
        [TestCase("IN PROGRESS")]
        public async Task UpdateSpecPullRequestAsync_PreservesRecordedInProgressRun(string generationStatus)
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Fields["Custom.GenerationStatusForJava"] = generationStatus;
            plan.Fields["Custom.SDKGenerationPipelineForJava"] = "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=99";
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(CreateApiSpecWorkItem(200, "https://github.com/Azure/azure-rest-api-specs/pull/123", "Active"));

            var result = await _devOpsService.UpdateSpecPullRequestAsync(
                100, new ReleasePlanSpecTarget { SpecPullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/456" }, [], [], CancellationToken.None);

            Assert.That(result, Is.True);
            var patch = _connection.CapturedPatches.Last().Document;
            Assert.That(patch, Has.Count.EqualTo(6));
            Assert.That(patch.Any(op => op.Path == "/fields/Custom.GenerationStatusForJava"), Is.False);
            Assert.That(patch.Skip(2).All(op => Equals(op.Value, "Not applicable")), Is.True);
        }

        [Test]
        public async Task UpdateSpecPullRequestAsync_AllLanguagesHaveRecordedRuns_DoesNotSendEmptyStatusPatch()
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            foreach (var language in new[] { "Dotnet", "Java", "JavaScript", "Go", "Python" })
            {
                plan.Fields[$"Custom.GenerationStatusFor{language}"] = "In progress";
                plan.Fields[$"Custom.SDKGenerationPipelineFor{language}"] = "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=99";
            }
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(CreateApiSpecWorkItem(200, "https://github.com/Azure/azure-rest-api-specs/pull/123", "Active"));

            var result = await _devOpsService.UpdateSpecPullRequestAsync(
                100, new ReleasePlanSpecTarget { SpecPullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/456" }, [], [], CancellationToken.None);

            Assert.That(result, Is.True);
            Assert.That(_connection.CapturedPatches, Has.Count.EqualTo(3));
            Assert.That(_connection.CapturedPatches[1].WorkItemId, Is.EqualTo(200));
            Assert.That(_connection.CapturedPatches.Last().Document.Any(op => op.Path.Contains("GenerationStatusFor")), Is.False);
        }

        #endregion

        #region Confirmed release target storage

        private const string TargetSha = "0123456789abcdef0123456789abcdef01234567";
        private const string PriorSha = "fedcba9876543210fedcba9876543210fedcba98";
        private const string TargetPr = "https://github.com/Azure/azure-rest-api-specs/pull/123";

        private (WorkItem Parent, WorkItem Spec, ReleasePlanSpecTarget Target) TargetFixture()
        {
            var parent = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            parent.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = PriorSha;
            var spec = CreateApiSpecWorkItemWithVersion(200, TargetPr, "Active", "2024-01-01");
            _connection.AddWorkItem(parent);
            _connection.AddWorkItem(spec);
            return (parent, spec, new ReleasePlanSpecTarget
            {
                SpecPullRequestUrl = TargetPr, SpecCommitSHA = TargetSha, ApiVersion = "2024-01-01",
                ExpectedTargetRevision = "100:1:200:1", SDKReleaseType = "beta"
            });
        }

        [Test]
        public async Task ReleaseTargetMapsParentPinChildVersionAndBothRevisions()
        {
            var (parent, spec, _) = TargetFixture();
            spec.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = "not-the-parent-pin";
            var result = await _devOpsService.GetReleasePlanForWorkItemAsync(100, default);
            Assert.That(result.SpecCommitSHA, Is.EqualTo(PriorSha));
            Assert.That(result.SpecAPIVersion, Is.EqualTo("2024-01-01"));
            Assert.That(result.ApiSpecWorkItemId, Is.EqualTo(200));
            Assert.That(result.TargetRevision, Is.EqualTo("100:1:200:1"));
            var stored = new ReleasePlanWorkItem { SpecCommitSHA = TargetSha };
            Assert.That(stored.GetPatchDocument().Single(op => op.Path == "/fields/Custom.SpecCommitSHA").Value, Is.EqualTo(TargetSha));
            Assert.That(stored.ToApiSpecWorkItem().GetPatchDocument().Any(op => op.Path == "/fields/Custom.SpecCommitSHA"), Is.False);
            parent.Fields.Remove(ReleasePlanWorkItem.SpecCommitSHAField);
            Assert.That((await _devOpsService.GetReleasePlanForWorkItemAsync(100, default)).SpecCommitSHA, Is.Empty);
            Assert.That(_connection.CapturedPatches, Is.Empty);
        }

        [TestCase("unreadable")]
        [TestCase("cleared-pin")]
        [TestCase("republished-pin")]
        public async Task ReleaseTargetReadFailsClosedWhenChildUnreadableOrPinChanges(string change)
        {
            var (parent, _, _) = TargetFixture();
            _connection.BeforeRead = id =>
            {
                if (id != 200) { return; }
                if (change == "unreadable") { throw new InvalidOperationException("API Spec unavailable"); }
                var updatedParent = new WorkItem
                {
                    Id = parent.Id,
                    Rev = parent.Rev + 2,
                    Fields = new Dictionary<string, object>(parent.Fields)
                };
                updatedParent.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = change == "cleared-pin" ? string.Empty : PriorSha;
                updatedParent.Fields["Custom.SDKtypetobereleased"] = "stable";
                _connection.AddWorkItem(updatedParent);
            };
            var result = await _devOpsService.GetReleasePlanForWorkItemAsync(100, default);
            Assert.That(result.SpecCommitSHA, Is.Empty);
            Assert.That(_connection.CapturedPatches, Is.Empty);
        }

        [TestCase("sha")]
        [TestCase("version")]
        [TestCase("fields")]
        [TestCase("packages")]
        public void ReleaseTargetPayloadRequiresRevisionBeforeAnyWrite(string payload)
        {
            var target = new ReleasePlanSpecTarget { SpecPullRequestUrl = TargetPr };
            if (payload == "sha") { target.SpecCommitSHA = TargetSha; }
            if (payload == "version") { target.ApiVersion = "2024-01-01"; }
            Dictionary<string, string> fields = payload == "fields" ? new() { ["Custom.SDKtypetobereleased"] = "beta" } : [];
            List<SDKInfo> packages = payload == "packages" ? [new() { Language = "Python", PackageName = "azure-contoso" }] : [];
            Assert.ThrowsAsync<InvalidOperationException>(() => _devOpsService.UpdateSpecPullRequestAsync(100, target, fields, packages, default));
            Assert.That(_connection.CapturedPatches, Is.Empty);
        }

        [TestCase("parent")]
        [TestCase("child")]
        [TestCase("missing-revision")]
        [TestCase("version")]
        public void ReleaseTargetRechecksRevisionAndVersionBeforeClearingPin(string change)
        {
            var (parent, spec, target) = TargetFixture();
            if (change == "parent") { parent.Rev++; }
            if (change == "child") { spec.Rev++; }
            if (change == "missing-revision") { spec.Rev = null; }
            if (change == "version") { target.ApiVersion = "2025-01-01"; }
            Assert.That(Assert.CatchAsync(() => _devOpsService.UpdateSpecPullRequestAsync(100, target, [], [], default)), Is.Not.Null);
            Assert.That(parent.Fields[ReleasePlanWorkItem.SpecCommitSHAField], Is.EqualTo(PriorSha));
            Assert.That(_connection.CapturedPatches, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ReleaseTargetPublishesPinAndMetadataLastPreservingStatusesOnlyForSameTarget(bool sameSha)
        {
            var (parent, spec, target) = TargetFixture();
            if (sameSha) { parent.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = TargetSha; }
            parent.Fields["Custom.SDKLanguages"] = "Java,Go";
            parent.Fields["Custom.JavaPackageName"] = "existing-java";
            parent.Fields["Custom.GenerationStatusForPython"] = "Pending";
            parent.Fields["Custom.ReleaseStatusForJava"] = "Released";
            parent.Fields["Custom.ReleaseExclusionStatusForJava"] = "Approved";
            parent.Fields["Custom.ReleaseExclusionStatusForPython"] = "MissingEmitterConfig";
            parent.Fields["Custom.GenerationStatusForJava"] = "In progress";
            parent.Fields["Custom.SDKGenerationPipelineForJava"] = "existing-pipeline";
            spec.Fields["Custom.RESTAPIReviews"] = $"<a href=\"{TargetPr}\">{TargetPr}</a>";
            Assert.That(await _devOpsService.UpdateSpecPullRequestAsync(100, target,
                new() { ["Custom.SDKtypetobereleased"] = "beta", ["Custom.ProductName"] = "Contoso" },
                [new() { Language = "Python", PackageName = "azure-contoso" }], default), Is.True);

            var patches = _connection.CapturedPatches;
            Assert.That(patches.Select(p => p.WorkItemId), Is.EqualTo(new[] { 100, 200, 100 }));
            Assert.That(patches.Select(p => p.Document[0].Path), Is.All.EqualTo("/rev"));
            Assert.That(patches.Select(p => p.Document[0].Value), Is.EqualTo(new object[] { 1, 1, 2 }));
            Assert.That(patches[0].Document[1].Value, Is.EqualTo(string.Empty));
            Assert.That(patches[1].Document.Single(op => op.Path == "/fields/Custom.APISpecversion").Value, Is.EqualTo("2024-01-01"));
            Assert.That(patches[2].Document.Any(op => op.Path.Contains("GenerationStatusFor")), Is.EqualTo(!sameSha));
            Assert.That(parent.Fields[ReleasePlanWorkItem.SpecCommitSHAField], Is.EqualTo(TargetSha));
            Assert.That(parent.Fields["Custom.ProductName"], Is.EqualTo("Contoso"));
            Assert.That(parent.Fields["Custom.SDKLanguages"].ToString()!.Split(','), Is.EquivalentTo(new[] { "Python", "Java", "Go" }));
            Assert.That(parent.Fields["Custom.JavaPackageName"], Is.EqualTo("existing-java"));
            Assert.That(parent.Fields["Custom.PythonPackageName"], Is.EqualTo("azure-contoso"));
            Assert.That(parent.Fields["Custom.GenerationStatusForPython"], Is.EqualTo(sameSha ? "Pending" : "Not applicable"));
            Assert.That(parent.Fields["Custom.GenerationStatusForJava"], Is.EqualTo("In progress"), "Keep the recorded run so generation can check whether it is still active.");
            Assert.That(parent.Fields["Custom.ReleaseStatusForJava"], Is.EqualTo("Released"));
            Assert.That(parent.Fields["Custom.ReleaseExclusionStatusForJava"], Is.EqualTo("Approved"));
            Assert.That(parent.Fields["Custom.ReleaseExclusionStatusForPython"], Is.EqualTo("Not applicable"));
            Assert.That(parent.Fields["Custom.SDKGenerationPipelineForJava"], Is.EqualTo("existing-pipeline"));
            Assert.That(spec.Fields["Custom.RESTAPIReviews"], Is.EqualTo($"<a href=\"{TargetPr}\">{TargetPr}</a>"));
        }

        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        public void ReleaseTargetPartialFailureOrRevisionConflictNeverRetriesOrRestoresPin(int failedPhase, bool conflict)
        {
            var (parent, spec, target) = TargetFixture();
            _connection.BeforePatch = (id, _) =>
            {
                if (_connection.CapturedPatches.Count != failedPhase) { return; }
                if (conflict) { if (id == 100) { parent.Rev++; } else { spec.Rev++; } }
                else { throw new InvalidOperationException("ADO unavailable"); }
            };
            Assert.That(Assert.CatchAsync(() => _devOpsService.UpdateSpecPullRequestAsync(100, target,
                new() { ["Custom.ProductName"] = "not-published" }, [], default)), Is.Not.Null);
            Assert.That(_connection.CapturedPatches, Has.Count.EqualTo(failedPhase));
            Assert.That(parent.Fields[ReleasePlanWorkItem.SpecCommitSHAField], Is.EqualTo(failedPhase == 1 ? PriorSha : string.Empty));
            Assert.That(parent.Fields.ContainsKey("Custom.ProductName"), Is.False);
        }

        [Test]
        public void ReleaseTargetRejectsMetadataPinOverrideBeforeClearingPin()
        {
            var (parent, _, target) = TargetFixture();
            Assert.ThrowsAsync<ArgumentException>(() => _devOpsService.UpdateSpecPullRequestAsync(100, target,
                new() { [ReleasePlanWorkItem.SpecCommitSHAField] = PriorSha }, [], default));
            Assert.That(parent.Fields[ReleasePlanWorkItem.SpecCommitSHAField], Is.EqualTo(PriorSha));
            Assert.That(_connection.CapturedPatches, Is.Empty);
        }

        [Test]
        public void ReleaseTargetCancellationLeavesPinClearedAndStopsPublication()
        {
            var (parent, _, target) = TargetFixture();
            using var cancellation = new CancellationTokenSource();
            _connection.BeforePatch = (id, _) => { if (id == 200) { cancellation.Cancel(); } };
            Assert.CatchAsync<OperationCanceledException>(() => _devOpsService.UpdateSpecPullRequestAsync(100, target, [], [], cancellation.Token));
            Assert.That(parent.Fields[ReleasePlanWorkItem.SpecCommitSHAField], Is.EqualTo(string.Empty));
            Assert.That(_connection.CapturedPatches, Has.Count.EqualTo(2));
        }

        #endregion

        #region Helper Methods

        private WorkItem CreateReleasePlanWorkItemWithReleasePlanId(int workItemId, int releasePlanId, string state)
        {
            return new WorkItem
            {
                Id = workItemId,
                Fields = new Dictionary<string, object>
                {
                    { "System.WorkItemType", "Release Plan" },
                    { "System.State", state },
                    { "System.Title", $"Release Plan {releasePlanId}" },
                    { "System.TeamProject", "internal" },
                    { "Custom.ReleasePlanID", releasePlanId.ToString() }
                },
                Relations = new List<WorkItemRelation>()
            };
        }

        private WorkItem CreateApiSpecWorkItem(int id, string pullRequestUrl, string state, int parentId = 100)
        {
            var workItem = new WorkItem
            {
                Id = id,
                Rev = 1,
                Fields = new Dictionary<string, object>
                {
                    { "System.WorkItemType", "API Spec" },
                    { "System.State", state },
                    { "Custom.ActiveSpecPullRequestUrl", pullRequestUrl },
                    { "System.TeamProject", "internal" }
                },
                Relations = new List<WorkItemRelation>
                {
                    new WorkItemRelation
                    {
                        Rel = "System.LinkTypes.Hierarchy-Reverse",
                        Url = $"https://dev.azure.com/azure-sdk/internal/_apis/wit/workItems/{parentId}"
                    }
                }
            };
            return workItem;
        }

        private WorkItem CreateApiSpecWorkItemWithVersion(int id, string pullRequestUrl, string state, string apiVersion, int parentId = 100)
        {
            var workItem = CreateApiSpecWorkItem(id, pullRequestUrl, state, parentId);
            workItem.Fields["Custom.APISpecversion"] = apiVersion;
            return workItem;
        }

        /// <summary>
        /// Creates a release plan work item with a Hierarchy-Forward relation pointing to a child API Spec work item.
        /// Required so GetApiSpecWorkItemAsync can traverse the parent→child link to read the API version.
        /// </summary>
        private WorkItem CreateReleasePlanWorkItemWithApiSpecChild(int id, string state, int apiSpecChildId)
        {
            var workItem = CreateReleasePlanWorkItem(id, state);
            workItem.Relations.Add(new WorkItemRelation
            {
                Rel = "System.LinkTypes.Hierarchy-Forward",
                Url = $"https://dev.azure.com/azure-sdk/internal/_apis/wit/workItems/{apiSpecChildId}"
            });
            return workItem;
        }

        private WorkItem CreateReleasePlanWorkItem(int id, string state)
        {
            var workItem = new WorkItem
            {
                Id = id,
                Rev = 1,
                Fields = new Dictionary<string, object>
                {
                    { "System.WorkItemType", "Release Plan" },
                    { "System.State", state },
                    { "System.Title", $"Release Plan {id}" },
                    { "System.TeamProject", "internal" },
                    { "Custom.ReleasePlanID", id.ToString() }
                },
                Relations = new List<WorkItemRelation>()
            };
            return workItem;
        }

        private WorkItem CreateReleasePlanWorkItemWithExclusionStatus(int id, string languageId, string exclusionStatus)
        {
            var workItem = CreateReleasePlanWorkItem(id, "In Progress");
            workItem.Fields[$"Custom.ReleaseExclusionStatusFor{languageId}"] = exclusionStatus;
            return workItem;
        }

        #endregion

        #region MapPackageWorkItemToModel PlannedReleases parsing Tests

        private static string CreateReleaseTable(string rows)
        {
            // GetMDVersionValue stores Markdown in a hidden div alongside its rendered HTML.
            return "<div style='display:none' id=__md>| Type | Version | Date |\n" +
                "| - | - | - |\n" + rows + "\n</div>";
        }

        private static WorkItem CreatePackageWorkItem(string plannedPackages, string version = "1.2.1")
        {
            return new WorkItem
            {
                Id = 1,
                Url = "https://dev.azure.com/azure-sdk/internal/_apis/wit/workItems/1",
                Fields = new Dictionary<string, object>
                {
                    { "System.WorkItemType", "Package" },
                    { "System.State", "Active" },
                    { "Custom.Package", "arm-computelimit" },
                    { "Custom.PackageVersion", version },
                    { "Custom.Language", "JavaScript" },
                    { "Custom.PlannedPackages", plannedPackages }
                },
                Relations = new List<WorkItemRelation>()
            };
        }

        [TestCase("2026-07-07")]
        [TestCase("07/07/2026")]
        public void MapPackageWorkItemToModel_ParsesPatchPlannedReleaseRow(string releaseDate)
        {
            // A Patch row must parse; previously the release-type allowlist (Beta|Stable|GA)
            // dropped it, causing a false "No planned release date found" readiness failure.
            var plannedPackages = CreateReleaseTable($"| Patch | 1.2.1 | {releaseDate} |");

            var model = DevOpsService.MapPackageWorkItemToModel(CreatePackageWorkItem(plannedPackages));

            Assert.That(model.PlannedReleases, Has.Count.EqualTo(1));
            var planned = model.PlannedReleases[0];
            Assert.Multiple(() =>
            {
                Assert.That(planned.ReleaseType, Is.EqualTo("Patch"));
                Assert.That(planned.Version, Is.EqualTo("1.2.1"));
                Assert.That(planned.ReleaseDate, Is.EqualTo(releaseDate));
            });
        }

        [Test]
        public void MapPackageWorkItemToModel_ParsesAllReleaseTypeLabels()
        {
            // All labels should parse, but the production header and separator must not.
            var plannedPackages = CreateReleaseTable(
                "| Beta | 1.0.0-beta.1 | 2026-07-01 |\n" +
                "| Stable | 1.0.0 | 2026-07-02 |\n" +
                "| GA | 1.3.0 | 2026-07-03 |\n" +
                "| Patch | 1.2.1 | 2026-07-04 |\n" +
                "| Hotfix | 1.3.0.post1 | 2026-07-05 |");

            var model = DevOpsService.MapPackageWorkItemToModel(CreatePackageWorkItem(plannedPackages));

            Assert.That(
                model.PlannedReleases.Select(r => r.ReleaseType),
                Is.EqualTo(new[] { "Beta", "Stable", "GA", "Patch", "Hotfix" }));
        }

        [TestCase("Custom.PlannedPackages")]
        [TestCase("Custom.ShippedPackages")]
        public void MapPackageWorkItemToModel_IgnoresReleaseTableWithoutDataRows(string field)
        {
            var workItem = CreatePackageWorkItem(string.Empty);
            workItem.Fields[field] = CreateReleaseTable(string.Empty);

            var model = DevOpsService.MapPackageWorkItemToModel(workItem);

            Assert.Multiple(() =>
            {
                Assert.That(model.PlannedReleases, Is.Empty);
                Assert.That(model.ReleasedVersions, Is.Empty);
            });
        }

        [Test]
        public void MapPackageWorkItemToModel_ParsesShippedReleaseWithoutTableMetadata()
        {
            var workItem = CreatePackageWorkItem(string.Empty);
            workItem.Fields["Custom.ShippedPackages"] = CreateReleaseTable("| Patch | 1.2.1 | 07/07/2026 |");

            var model = DevOpsService.MapPackageWorkItemToModel(workItem);

            Assert.That(model.ReleasedVersions, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(model.ReleasedVersions[0].ReleaseType, Is.EqualTo("Patch"));
                Assert.That(model.ReleasedVersions[0].Version, Is.EqualTo("1.2.1"));
                Assert.That(model.ReleasedVersions[0].ReleaseDate, Is.EqualTo("07/07/2026"));
            });
        }

        #endregion

        #region GetReleasePlansForPackageAsync Tests

        [TestCase("python", "Python")]
        [TestCase(".net", "Dotnet")]
        [TestCase("csharp", "Dotnet")]
        [TestCase("javascript", "JavaScript")]
        [TestCase("java", "Java")]
        [TestCase("go", "Go")]
        public async Task GetReleasePlansForPackageAsync_QueryIncludesReleaseStatusFilter(string language, string expectedLanguageId)
        {
            // Arrange
            var packageName = "azure-test-package";
            var releasePlanWorkItem = CreateReleasePlanWorkItemForPackage(100, packageName, language);
            _connection.AddWorkItemToQuery(releasePlanWorkItem);

            // Act
            await _devOpsService.GetReleasePlansForPackageAsync(packageName, language, false, CancellationToken.None);

            // Assert - verify query includes the release status filter
            var capturedQuery = _connection.LastCapturedQuery;
            Assert.That(capturedQuery, Is.Not.Null, "Expected a WIQL query to be captured");
            Assert.That(capturedQuery, Does.Contain($"[Custom.ReleaseStatusFor{expectedLanguageId}] <> 'Released'"),
                $"Query should filter out already-released packages for language '{language}'");
        }

        [Test]
        public async Task GetReleasePlansForPackageAsync_QueryIncludesPackageNameFilter()
        {
            // Arrange
            var packageName = "azure-test-package";
            var releasePlanWorkItem = CreateReleasePlanWorkItemForPackage(100, packageName, "python");
            _connection.AddWorkItemToQuery(releasePlanWorkItem);

            // Act
            await _devOpsService.GetReleasePlansForPackageAsync(packageName, "python", false, CancellationToken.None);

            // Assert
            var capturedQuery = _connection.LastCapturedQuery;
            Assert.That(capturedQuery, Does.Contain($"[Custom.PythonPackageName] = '{packageName}'"));
        }

        [Test]
        public async Task GetReleasePlansForPackageAsync_QueryIncludesInProgressStateFilter()
        {
            // Arrange
            var packageName = "azure-test-package";
            var releasePlanWorkItem = CreateReleasePlanWorkItemForPackage(100, packageName, "python");
            _connection.AddWorkItemToQuery(releasePlanWorkItem);

            // Act
            await _devOpsService.GetReleasePlansForPackageAsync(packageName, "python", false, CancellationToken.None);

            // Assert
            var capturedQuery = _connection.LastCapturedQuery;
            Assert.That(capturedQuery, Does.Contain("[System.State] = 'In Progress'"));
        }

        [Test]
        public async Task GetReleasePlansForPackageAsync_ReturnsEmptyList_WhenNoMatchingWorkItems()
        {
            // Arrange - no work items added to query results

            // Act
            var result = await _devOpsService.GetReleasePlansForPackageAsync("azure-test-package", "python", false, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task GetReleasePlansForPackageAsync_TestReleasePlan_QueryContainsTestTag()
        {
            // Arrange
            var packageName = "azure-test-package";
            var releasePlanWorkItem = CreateReleasePlanWorkItemForPackage(100, packageName, "python");
            _connection.AddWorkItemToQuery(releasePlanWorkItem);

            // Act
            await _devOpsService.GetReleasePlansForPackageAsync(packageName, "python", isTestReleasePlan: true, CancellationToken.None);

            // Assert
            var capturedQuery = _connection.LastCapturedQuery;
            Assert.That(capturedQuery, Does.Contain("[System.Tags] CONTAINS"));
            Assert.That(capturedQuery, Does.Contain("Release Planner App Test"));
        }

        [Test]
        public async Task GetReleasePlansForPackageAsync_NonTestReleasePlan_QueryExcludesTestTag()
        {
            // Arrange
            var packageName = "azure-test-package";
            var releasePlanWorkItem = CreateReleasePlanWorkItemForPackage(100, packageName, "python");
            _connection.AddWorkItemToQuery(releasePlanWorkItem);

            // Act
            await _devOpsService.GetReleasePlansForPackageAsync(packageName, "python", isTestReleasePlan: false, CancellationToken.None);

            // Assert
            var capturedQuery = _connection.LastCapturedQuery;
            Assert.That(capturedQuery, Does.Contain("[System.Tags] NOT CONTAINS"));
            Assert.That(capturedQuery, Does.Contain("Release Planner App Test"));
        }

        [Test]
        public async Task GetReleasePlansForPackageAsync_EscapesSingleQuoteInPackageName()
        {
            // Arrange
            var packageName = "azure-test's-package";
            var releasePlanWorkItem = CreateReleasePlanWorkItemForPackage(100, packageName, "python");
            _connection.AddWorkItemToQuery(releasePlanWorkItem);

            // Act
            await _devOpsService.GetReleasePlansForPackageAsync(packageName, "python", false, CancellationToken.None);

            // Assert
            var capturedQuery = _connection.LastCapturedQuery;
            Assert.That(capturedQuery, Does.Contain("azure-test''s-package"), "Single quotes should be escaped in WIQL query");
        }

        private WorkItem CreateReleasePlanWorkItemForPackage(int id, string packageName, string language)
        {
            var languageId = DevOpsService.MapLanguageToId(language);
            var workItem = new WorkItem
            {
                Id = id,
                Fields = new Dictionary<string, object>
                {
                    { "System.WorkItemType", "Release Plan" },
                    { "System.State", "In Progress" },
                    { "System.Title", $"Release Plan {id}" },
                    { "System.TeamProject", "internal" },
                    { "Custom.ReleasePlanID", id.ToString() },
                    { $"Custom.{languageId}PackageName", packageName },
                    { $"Custom.ReleaseStatusFor{languageId}", "" }
                },
                Relations = new List<WorkItemRelation>()
            };
            return workItem;
        }

        #endregion

        #region RunSDKGenerationPipelineAsync Tests

        [Test]
        public void RunSDKGenerationPipelineAsync_WhenRunningInAzurePipelines_DoesNotIncludeSdkReleaseTypeOrApiVersionTemplateParams()
        {
            // Arrange
            var method = typeof(DevOpsService).GetMethod("BuildSdkGenerationTemplateParams", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            // Act
            var templateParams = (Dictionary<string, string>)method!.Invoke(null, ["specification/test/service", 0, "stable", "v1", "feature/sdk-branch", true])!;

            // Assert
            Assert.That(templateParams, Contains.Key("ConfigType"));
            Assert.That(templateParams, Contains.Key("ConfigPath"));
            Assert.That(templateParams, Contains.Key("CreatePullRequest"));
            Assert.That(templateParams, Contains.Key("ReleasePlanWorkItemId"));
            Assert.That(templateParams, Contains.Key("TriggerSource"));
            Assert.That(templateParams, Contains.Key("SdkRepoBranch"));
            Assert.That(templateParams["SdkRepoBranch"], Is.EqualTo("feature/sdk-branch"));
            Assert.That(templateParams, Does.Not.ContainKey("SdkReleaseType"));
            Assert.That(templateParams, Does.Not.ContainKey("ApiVersion"));
        }

        #endregion

        #region FindPackageWorkItemIdsAsync Tests

        [Test]
        public async Task FindPackageWorkItemIdsAsync_GoLanguage_QueryUsesInConditionForBothCases()
        {
            // Arrange - no work items needed, just capture the query
            // Act
            await _devOpsService.FindPackageWorkItemIdsAsync("azure-sdk-go", "go", "1.0", CancellationToken.None);

            // Assert
            var capturedQuery = _connection.LastCapturedQuery;
            Assert.That(capturedQuery, Does.Contain("[Custom.Language] IN ('Go', 'go')"),
                "Go language query should search for both 'Go' and 'go' to handle ADO case inconsistency");
        }

        [Test]
        public async Task FindPackageWorkItemIdsAsync_PythonLanguage_QueryUsesInConditionForBothCases()
        {
            // Arrange - no work items needed, just capture the query
            // Act
            await _devOpsService.FindPackageWorkItemIdsAsync("azure-core", "Python", "1.0", CancellationToken.None);

            // Assert
            var capturedQuery = _connection.LastCapturedQuery;
            Assert.That(capturedQuery, Does.Contain("[Custom.Language] IN ('Python', 'python')"),
                "Language query should search for both canonical and lowercase forms to handle ADO case inconsistency");
        }

        #endregion

        #region ListPartialPackageWorkItemAsync Tests

        [Test]
        public async Task ListPartialPackageWorkItemAsync_GoLanguage_QueryUsesInConditionForBothCases()
        {
            // Arrange - no work items needed, just capture the query
            // Act
            await _devOpsService.ListPartialPackageWorkItemAsync("azure-sdk-go", "go", CancellationToken.None);

            // Assert
            var capturedQuery = _connection.LastCapturedQuery;
            Assert.That(capturedQuery, Does.Contain("[Custom.Language] IN ('Go', 'go')"),
                "Go language query should search for both 'Go' and 'go' to handle ADO case inconsistency");
        }

        [Test]
        public async Task ListPartialPackageWorkItemAsync_PythonLanguage_QueryUsesInConditionForBothCases()
        {
            // Arrange - no work items needed, just capture the query
            // Act
            await _devOpsService.ListPartialPackageWorkItemAsync("azure-core", "Python", CancellationToken.None);

            // Assert
            var capturedQuery = _connection.LastCapturedQuery;
            Assert.That(capturedQuery, Does.Contain("[Custom.Language] IN ('Python', 'python')"),
                "Language query should search for both canonical and lowercase forms to handle ADO case inconsistency");
        }

        #endregion

        #region GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync Tests

        [Test]
        public async Task GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync_ReturnsNullWhenNoReleasePlanExists()
        {
            // Arrange: no release plans in the system
            var typeSpecPath = "specification/contoso/Contoso.Management";
            var apiVersion = "2024-01-01";

            // Act
            var result = await _devOpsService.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecPath, apiVersion, ApiReleaseType.GA, CancellationToken.None);

            // Assert
            Assert.IsNull(result, "Should return null when no release plans exist for the TypeSpec path");
        }

        [Test]
        public async Task GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync_ReturnsNullWhenApiVersionDoesNotMatch()
        {
            // Arrange: release plan exists but with different API version
            var typeSpecPath = "specification/contoso/Contoso.Management";
            var requestedApiVersion = "2024-01-01";
            var existingApiVersion = "2023-06-01";
            
            var releasePlan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            var apiSpec = CreateApiSpecWorkItemWithVersion(200, "https://github.com/Azure/azure-rest-api-specs/pull/12345", "Active", 
                existingApiVersion, parentId: 100);
            
            _connection.AddWorkItemToQuery(releasePlan);
            _connection.AddWorkItem(releasePlan);
            _connection.AddWorkItem(apiSpec);

            // Act
            var result = await _devOpsService.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecPath, requestedApiVersion, ApiReleaseType.GA, CancellationToken.None);

            // Assert
            Assert.IsNull(result, "Should return null when API version does not match");
        }

        [Test]
        public async Task GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync_ReturnsReleasePlanWhenApiVersionMatches()
        {
            // Arrange: release plan with matching API version
            var typeSpecPath = "specification/contoso/Contoso.Management";
            var apiVersion = "2024-01-01";
            
            var releasePlan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            releasePlan.Fields["Custom.ReleasePlanType"] = "GA";
            var apiSpec = CreateApiSpecWorkItemWithVersion(200, "https://github.com/Azure/azure-rest-api-specs/pull/12345", "Active", 
                apiVersion, parentId: 100);
            releasePlan.Fields["Custom.ApiSpecProjectPath"] = typeSpecPath;
            
            _connection.AddWorkItemToQuery(releasePlan);
            _connection.AddWorkItem(releasePlan);
            _connection.AddWorkItem(apiSpec);

            // Act
            var result = await _devOpsService.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecPath, apiVersion, ApiReleaseType.GA, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result, "Should return release plan when API version matches");
            Assert.That(result!.WorkItemId, Is.EqualTo(100));
            Assert.That(result.SpecAPIVersion, Is.EqualTo(apiVersion));
        }

        [Test]
        public async Task GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync_LoopsToFindMatchingApiVersionWhenMultipleExist()
        {
            // Arrange: multiple release plans with different API versions
            var typeSpecPath = "specification/contoso/Contoso.Management";
            var requestedApiVersion = "2024-01-01";
            
            var releasePlan1 = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            releasePlan1.Fields["Custom.ApiSpecProjectPath"] = typeSpecPath;
            releasePlan1.Fields["Custom.ReleasePlanType"] = "GA";
            var apiSpec1 = CreateApiSpecWorkItemWithVersion(200, "https://github.com/Azure/azure-rest-api-specs/pull/12345", "Active", 
                "2023-06-01", parentId: 100);
            
            var releasePlan2 = CreateReleasePlanWorkItemWithApiSpecChild(101, "In Progress", 201);
            releasePlan2.Fields["Custom.ApiSpecProjectPath"] = typeSpecPath;
            releasePlan2.Fields["Custom.ReleasePlanType"] = "GA";
            var apiSpec2 = CreateApiSpecWorkItemWithVersion(201, "https://github.com/Azure/azure-rest-api-specs/pull/12346", "Active", 
                requestedApiVersion, parentId: 101);
            
            _connection.AddWorkItemToQuery(releasePlan1);
            _connection.AddWorkItemToQuery(releasePlan2);
            _connection.AddWorkItem(releasePlan1);
            _connection.AddWorkItem(releasePlan2);
            _connection.AddWorkItem(apiSpec1);
            _connection.AddWorkItem(apiSpec2);

            // Act
            var result = await _devOpsService.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecPath, requestedApiVersion, ApiReleaseType.GA, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result, "Should find matching release plan even when multiple exist");
            Assert.That(result!.WorkItemId, Is.EqualTo(101), "Should return the release plan with matching API version");
            Assert.That(result.SpecAPIVersion, Is.EqualTo(requestedApiVersion));
        }

        [Test]
        public async Task GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync_ApiVersionMatchingIsCaseInsensitive()
        {
            // Arrange: API version with different case
            var typeSpecPath = "specification/contoso/Contoso.Management";
            var requestedApiVersion = "2024-01-01";
            var existingApiVersion = "2024-01-01"; // same version
            
            var releasePlan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            releasePlan.Fields["Custom.ApiSpecProjectPath"] = typeSpecPath;
            releasePlan.Fields["Custom.ReleasePlanType"] = "GA";
            var apiSpec = CreateApiSpecWorkItemWithVersion(200, "https://github.com/Azure/azure-rest-api-specs/pull/12345", "Active", 
                existingApiVersion, parentId: 100);
            
            _connection.AddWorkItemToQuery(releasePlan);
            _connection.AddWorkItem(releasePlan);
            _connection.AddWorkItem(apiSpec);

            // Act
            var result = await _devOpsService.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecPath, requestedApiVersion, ApiReleaseType.GA, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result, "Should match API version case-insensitively");
        }

        [Test]
        public async Task GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync_ReturnsNullWhenApiVersionIsEmpty()
        {
            // Arrange
            var typeSpecPath = "specification/contoso/Contoso.Management";
            var apiVersion = "";

            // Act
            var result = await _devOpsService.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecPath, apiVersion, ApiReleaseType.GA, CancellationToken.None);

            // Assert
            Assert.IsNull(result, "Should return null when API version is empty");
        }

        [Test]
        public async Task GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync_ReturnsNullWhenApiReleaseTypeDoesNotMatch()
        {
            var typeSpecPath = "specification/contoso/Contoso.Management";
            var apiVersion = "2024-01-01";
            var releasePlan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            releasePlan.Fields["Custom.ApiSpecProjectPath"] = typeSpecPath;
            releasePlan.Fields["Custom.ReleasePlanType"] = "APEX Public Preview";
            var apiSpec = CreateApiSpecWorkItemWithVersion(200, "https://github.com/Azure/azure-rest-api-specs/pull/12345", "Active",
                apiVersion, parentId: 100);

            _connection.AddWorkItemToQuery(releasePlan);
            _connection.AddWorkItem(releasePlan);
            _connection.AddWorkItem(apiSpec);

            var result = await _devOpsService.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(
                typeSpecPath, apiVersion, ApiReleaseType.GA, CancellationToken.None);

            Assert.IsNull(result, "Should return null when API release type does not match");
            Assert.That(_connection.LastCapturedQuery, Does.Contain("[Custom.ReleasePlanType] = 'GA'"));
        }

        [Test]
        public void GetActiveReleasePlansByTypeSpecProjectPathAsync_PropagatesCancellationWhileMapping()
        {
            var releasePlanWorkItem = CreateReleasePlanWorkItem(100, "In Progress");
            releasePlanWorkItem.Fields["Custom.ApiSpecProjectPath"] = "specification/contoso/Contoso.Management";
            _connection.AddWorkItemToQuery(releasePlanWorkItem);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _devOpsService.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                    "specification/contoso/Contoso.Management",
                    ct: cts.Token));
        }

        [Test]
        public void GetActiveReleasePlansByTypeSpecProjectPathAsync_PropagatesCancellationFromQuery()
        {
            _connection.CancelQuery();

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _devOpsService.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                    "specification/contoso/Contoso.Management"));
        }

        [Test]
        public void GetActiveReleasePlansByTypeSpecProjectPathAsync_PropagatesCancellationFromBulkFetch()
        {
            var releasePlanWorkItem = CreateReleasePlanWorkItem(100, "In Progress");
            releasePlanWorkItem.Fields["Custom.ApiSpecProjectPath"] = "specification/contoso/Contoso.Management";
            _connection.AddWorkItemToQuery(releasePlanWorkItem);
            _connection.CancelBulkFetch();

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _devOpsService.GetActiveReleasePlansByTypeSpecProjectPathAsync(
                    "specification/contoso/Contoso.Management"));
        }

        #endregion
        #region TestDevOpsConnection

        private class TestDevOpsConnection : IDevOpsConnection
        {
            private readonly TestWorkItemClient _workItemClient = new();

            public string? LastCapturedQuery => _workItemClient.LastCapturedQuery;

            public Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument? LastCapturedPatchDocument => _workItemClient.LastCapturedPatchDocument;

            public int WorkItemUpdateCount => _workItemClient.UpdateCount;

            public List<(int WorkItemId, DevOpsJsonPatchDocument Document)> CapturedPatches => _workItemClient.CapturedPatches;

            public Action<int>? BeforeRead { set => _workItemClient.BeforeRead = value; }
            public Action<int, DevOpsJsonPatchDocument>? BeforePatch { set => _workItemClient.BeforePatch = value; }

            public BuildHttpClient GetBuildClient(CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }

            public Azure.Core.AccessToken GetToken(CancellationToken ct)
            {
                throw new NotImplementedException();
            }

            public BuildHttpClient GetAnonymousBuildClient()
            {
                throw new NotImplementedException();
            }

            public WorkItemTrackingHttpClient GetWorkItemClient(CancellationToken ct = default)
            {
                return _workItemClient;
            }

            public ProjectHttpClient GetProjectClient(CancellationToken ct = default)
            {
                throw new NotImplementedException();
            }

            public void AddWorkItemToQuery(WorkItem workItem)
            {
                _workItemClient.AddWorkItemToQuery(workItem);
            }

            public void AddWorkItem(WorkItem workItem)
            {
                _workItemClient.AddWorkItem(workItem);
            }

            public void CancelQuery()
            {
                _workItemClient.CancelQuery = true;
            }

            public void CancelBulkFetch()
            {
                _workItemClient.CancelBulkFetch = true;
            }

            public void FailNextRelationUpdate(bool addRelationBeforeFailure)
            {
                _workItemClient.FailRelationUpdate = true;
                _workItemClient.AddRelationBeforeFailure = addRelationBeforeFailure;
            }
        }

        private class TestWorkItemClient : WorkItemTrackingHttpClient
        {
            private readonly List<WorkItem> _queryWorkItems = new();
            private readonly Dictionary<int, WorkItem> _workItems = new();

            public string? LastCapturedQuery { get; private set; }

            public Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument? LastCapturedPatchDocument { get; private set; }

            public List<(int WorkItemId, DevOpsJsonPatchDocument Document)> CapturedPatches { get; } = [];

            public Action<int>? BeforeRead { get; set; }
            public Action<int, DevOpsJsonPatchDocument>? BeforePatch { get; set; }

            public bool CancelQuery { get; set; }

            public bool CancelBulkFetch { get; set; }
            public bool FailRelationUpdate { get; set; }
            public bool AddRelationBeforeFailure { get; set; }

            public int UpdateCount { get; private set; }

            public TestWorkItemClient() : base(new Uri("https://dev.azure.com/test"), null)
            {
            }

            public void AddWorkItemToQuery(WorkItem workItem)
            {
                _queryWorkItems.Add(workItem);
            }

            public void AddWorkItem(WorkItem workItem)
            {
                if (workItem.Id.HasValue)
                {
                    _workItems[workItem.Id.Value] = workItem;
                }
            }

            public override Task<WorkItemQueryResult> QueryByWiqlAsync(
                Wiql wiql,
                string? project = null,
                bool? timePrecision = null,
                int? top = null,
                object? userState = null,
                CancellationToken cancellationToken = default)
            {
                LastCapturedQuery = wiql?.Query;
                if (CancelQuery)
                {
                    return Task.FromCanceled<WorkItemQueryResult>(new CancellationToken(canceled: true));
                }
                var result = new WorkItemQueryResult
                {
                    WorkItems = _queryWorkItems.Select(wi => new WorkItemReference { Id = wi.Id ?? 0 }).ToList()
                };
                return Task.FromResult(result);
            }


            public override Task<WorkItemQueryResult> QueryByWiqlAsync(
                Wiql wiql,
                bool? timePrecision = null,
                int? top = null,
                object? userState = null,
                CancellationToken cancellationToken = default)
            {
                LastCapturedQuery = wiql?.Query;
                if (CancelQuery)
                {
                    return Task.FromCanceled<WorkItemQueryResult>(new CancellationToken(canceled: true));
                }
                var result = new WorkItemQueryResult
                {
                    WorkItems = _queryWorkItems.Select(wi => new WorkItemReference { Id = wi.Id ?? 0 }).ToList()
                };
                return Task.FromResult(result);
            }


            public override Task<List<WorkItem>> GetWorkItemsAsync(IEnumerable<int> ids, IEnumerable<string>? fields = null, DateTime? asOf = null, WorkItemExpand? expand = null, WorkItemErrorPolicy? errorPolicy = null, object? userState = null, CancellationToken cancellationToken = default(CancellationToken))
            {
                if (CancelBulkFetch)
                {
                    return Task.FromCanceled<List<WorkItem>>(new CancellationToken(canceled: true));
                }
                var workItems = _queryWorkItems.Where(wi => ids.Contains(wi.Id ?? 0)).ToList();
                return Task.FromResult(workItems);
            }

            public override Task<WorkItem> GetWorkItemAsync(string project, int id, IEnumerable<string>? fields = null, DateTime? asOf = null, WorkItemExpand? expand = null, object? userState = null, CancellationToken cancellationToken = default(CancellationToken))
            {
                if (_workItems.TryGetValue(id, out var workItem))
                {
                    return Task.FromResult(workItem);
                }
                throw new InvalidOperationException($"Work item {id} not found");
            }


            public override Task<WorkItem> GetWorkItemAsync(
                int id,
                IEnumerable<string>? fields = null,
                DateTime? asOf = null,
                WorkItemExpand? expand = null,
                object? userState = null,
                CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromCanceled<WorkItem>(cancellationToken);
                }
                BeforeRead?.Invoke(id);
                if (_workItems.TryGetValue(id, out var workItem))
                {
                    return Task.FromResult(workItem);
                }
                throw new InvalidOperationException($"Work item {id} not found");
            }

            public override Task<WorkItem> UpdateWorkItemAsync(
                Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument document,
                int id,
                bool? validateOnly = null,
                bool? bypassRules = null,
                bool? suppressNotifications = null,
                WorkItemExpand? expand = null,
                object? userState = null,
                CancellationToken cancellationToken = default)
            {
                UpdateCount++;
                LastCapturedPatchDocument = document;
                CapturedPatches.Add((id, document));
                BeforePatch?.Invoke(id, document);
                cancellationToken.ThrowIfCancellationRequested();
                _workItems.TryGetValue(id, out var workItem);
                workItem ??= new WorkItem { Id = id, Fields = new Dictionary<string, object>() };
                foreach (var test in document.Where(op => op.Operation == Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Test))
                {
                    if (test.Path == "/rev" && !Equals(test.Value, workItem.Rev))
                    {
                        throw new InvalidOperationException("Revision conflict");
                    }
                }
                foreach (var field in document.Where(op => op.Operation == Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add
                    && op.Path.StartsWith("/fields/", StringComparison.Ordinal)))
                {
                    workItem.Fields[field.Path[8..]] = field.Value;
                }
                workItem.Rev = (workItem.Rev ?? 0) + 1;
                foreach (var operation in document.Where(operation => operation.Path == "/relations/-"))
                {
                    if (!FailRelationUpdate || AddRelationBeforeFailure)
                    {
                        workItem.Relations ??= new List<WorkItemRelation>();
                        workItem.Relations.Add((WorkItemRelation)operation.Value);
                    }
                    if (FailRelationUpdate)
                    {
                        FailRelationUpdate = false;
                        throw new VssServiceException("Relation update failed");
                    }
                }
                return Task.FromResult(workItem);
            }

        }

        #endregion
    }
}
