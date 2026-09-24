// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Notification;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Moq;
using DevOpsJsonPatchDocument = Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument;
using DevOpsPatchOperation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation;

namespace Azure.Sdk.Tools.Cli.Tests.Services
{
    [TestFixture]
    public class DevOpsServiceTests
    {
        private const string SpecCommit = "0123456789abcdef0123456789abcdef01234567";
        private const string PreviousSpecCommit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string SpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/123";
        private const string NewSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/456";
        private const string SpecApiVersion = "2026-01-01-preview";
        private const string ConfirmedApiVersion = "2026-01-01";
        private const string ConfirmedProjectPath = "specification/testcontoso/Contoso.Management";
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

        [TestCase("Pending")]
        [TestCase("In progress")]
        [TestCase("Completed")]
        public async Task UpdateSpecPullRequestAsync_SameLinkPinUpdatePreservesAllStatusAndHistory(string status)
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = PreviousSpecCommit;
            foreach (var language in new[] { "Dotnet", "Java", "JavaScript", "Go", "Python" })
            {
                plan.Fields[$"Custom.GenerationStatusFor{language}"] = status;
                plan.Fields[$"Custom.SDKGenerationPipelineFor{language}"] = "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=99";
                plan.Fields[$"Custom.SDKPullRequestFor{language}"] = "existing-sdk-pr";
                plan.Fields[$"Custom.SDKPullRequestStatusFor{language}"] = "Merged";
                plan.Fields[$"Custom.ReleaseStatusFor{language}"] = "Released";
                plan.Fields[$"Custom.ReleaseExclusionStatusFor{language}"] = "Approved";
            }
            var expectedParentFields = new Dictionary<string, object>(plan.Fields)
            {
                [ReleasePlanWorkItem.SpecCommitSHAField] = SpecCommit
            };
            var spec = CreateApiSpecWorkItemWithVersion(200, SpecPullRequest, "Active", SpecApiVersion);
            var existingLinks = $"<a href=\"older-spec-pr\">older-spec-pr</a><br><a href=\"{SpecPullRequest}\">{SpecPullRequest}</a>";
            spec.Fields["Custom.RESTAPIReviews"] = existingLinks;
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(spec);

            Assert.That(await _devOpsService.UpdateSpecPullRequestAsync(
                100, SpecPullRequest, SpecCommit, PreviousSpecCommit, SpecApiVersion, CancellationToken.None), Is.True);

            Assert.That(_connection.CapturedPatches.Select(p => p.WorkItemId), Is.EqualTo(new[] { 100, 200, 100 }));
            foreach (var patch in _connection.CapturedPatches.Where(p => p.WorkItemId == 100))
            {
                Assert.That(patch.Document.Select(p => p.Path), Is.EqualTo(new[] { "/rev", $"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}" }),
                    "Same-link updates must only clear and publish the pin, without writing any status or history fields.");
            }
            Assert.That(_connection.CapturedPatches[1].Document.Single(p => p.Path == "/fields/Custom.RESTAPIReviews").Value, Is.EqualTo(existingLinks));
            Assert.That(_connection.GetStoredWorkItem(100).Fields, Is.EquivalentTo(expectedParentFields));
            Assert.That(_connection.GetStoredWorkItem(200).Fields, Is.EquivalentTo(spec.Fields));
        }

        [TestCase("")]
        [TestCase(SpecCommit)]
        [TestCase("not-a-commit")]
        public void UpdateSpecPullRequestAsync_RejectsStaleExpectedParentPinBeforeAnyWrite(string expectedPin)
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = PreviousSpecCommit;
            var spec = CreateApiSpecWorkItem(200, SpecPullRequest, "Active");
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(spec);

            var error = Assert.ThrowsAsync<Exception>(() => _devOpsService.UpdateSpecPullRequestAsync(
                100, NewSpecPullRequest, SpecCommit, expectedPin, SpecApiVersion, CancellationToken.None));

            Assert.That(error!.Message, Does.Contain("spec commit changed"));
            Assert.That(_connection.CapturedPatches, Is.Empty);
            Assert.That(_connection.GetStoredWorkItem(100).Fields, Is.EquivalentTo(plan.Fields));
            Assert.That(_connection.GetStoredWorkItem(200).Fields, Is.EquivalentTo(spec.Fields));
        }

        [TestCase("main")]
        [TestCase("abc123")]
        [TestCase("gggggggggggggggggggggggggggggggggggggggg")]
        public void UpdateSpecPullRequestAsync_RejectsInvalidNewPinBeforeAnyWrite(string commitSha)
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = PreviousSpecCommit;
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(CreateApiSpecWorkItem(200, SpecPullRequest, "Active"));

            var error = Assert.ThrowsAsync<Exception>(() => _devOpsService.UpdateSpecPullRequestAsync(
                100, NewSpecPullRequest, commitSha, PreviousSpecCommit, SpecApiVersion, CancellationToken.None));

            Assert.That(error!.Message, Does.Contain("40-character hexadecimal commit SHA"));
            Assert.That(_connection.CapturedPatches, Is.Empty);
            Assert.That(_connection.GetStoredWorkItem(100).Fields, Is.EquivalentTo(plan.Fields));
        }

        [TestCase("")]
        [TestCase(SpecApiVersion)]
        [TestCase("2026-01-01-PREVIEW")]
        public async Task UpdateSpecPullRequestAsync_UpdatesChildTargetBetweenParentClearAndPublish(string currentApiVersion)
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Rev = 4;
            plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = PreviousSpecCommit;
            var spec = CreateApiSpecWorkItemWithVersion(200, SpecPullRequest, "Active", currentApiVersion);
            spec.Rev = 7;
            spec.Fields["Custom.RESTAPIReviews"] = $"<a href=\"{SpecPullRequest}\">{SpecPullRequest}</a>";
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(spec);

            Assert.That(await _devOpsService.UpdateSpecPullRequestAsync(
                100, NewSpecPullRequest, SpecCommit, PreviousSpecCommit.ToUpperInvariant(), SpecApiVersion, CancellationToken.None), Is.True);

            Assert.That(_connection.CapturedPatches.Select(p => p.WorkItemId), Is.EqualTo(new[] { 100, 200, 100 }));
            var clearPatch = _connection.CapturedPatches[0].Document;
            AssertRevisionGuard(clearPatch, 4);
            Assert.That(clearPatch, Has.Count.EqualTo(2));
            Assert.That(clearPatch[1].Path, Is.EqualTo($"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}"));
            Assert.That(clearPatch[1].Value, Is.EqualTo(string.Empty));

            var childPatch = _connection.CapturedPatches[1].Document;
            AssertRevisionGuard(childPatch, 7);
            Assert.That(childPatch.Single(p => p.Path == "/fields/Custom.ActiveSpecPullRequestUrl").Value, Is.EqualTo(NewSpecPullRequest));
            Assert.That(childPatch.Single(p => p.Path == "/fields/Custom.APISpecversion").Value, Is.EqualTo(SpecApiVersion));
            Assert.That(childPatch.Single(p => p.Path == "/fields/Custom.RESTAPIReviews").Value,
                Is.EqualTo($"<a href=\"{SpecPullRequest}\">{SpecPullRequest}</a><br><a href=\"{NewSpecPullRequest}\">{NewSpecPullRequest}</a>"));
            Assert.That(childPatch.Any(p => p.Path.Equals($"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}", StringComparison.OrdinalIgnoreCase)), Is.False);

            var publishPatch = _connection.CapturedPatches[2].Document;
            AssertRevisionGuard(publishPatch, 5);
            Assert.That(publishPatch.Single(p => p.Path == $"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}").Value, Is.EqualTo(SpecCommit));
            Assert.That(_connection.UpdatedWorkItems.Select(w => w.Rev), Is.EqualTo(new[] { 5, 8, 6 }));
            Assert.That(_connection.UpdatedWorkItems[0].Fields[ReleasePlanWorkItem.SpecCommitSHAField], Is.EqualTo(string.Empty),
                "The saved clear response must remain an independent snapshot after the parent pin is published.");
            Assert.That(_connection.UpdatedWorkItems[1].Fields["Custom.APISpecversion"], Is.EqualTo(SpecApiVersion));
            Assert.That(_connection.GetStoredWorkItem(200).Fields.ContainsKey(ReleasePlanWorkItem.SpecCommitSHAField), Is.False);

            var result = await _devOpsService.GetReleasePlanForWorkItemAsync(100, CancellationToken.None);
            Assert.That(result.SpecCommitSHA, Is.EqualTo(SpecCommit));
            Assert.That(result.SpecAPIVersion, Is.EqualTo(SpecApiVersion));
            Assert.That(result.ActiveSpecPullRequest, Is.EqualTo(NewSpecPullRequest));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void UpdateSpecPullRequestAsync_RevisionConflictAtEachPhaseDoesNotRetryOrRestorePin(int failedPhase)
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = PreviousSpecCommit;
            plan.Fields["Custom.GenerationStatusForJava"] = "Completed";
            plan.Fields["Custom.SDKGenerationPipelineForJava"] = "existing-pipeline";
            var spec = CreateApiSpecWorkItemWithVersion(200, SpecPullRequest, "Active", "");
            var existingLink = $"<a href=\"{SpecPullRequest}\">{SpecPullRequest}</a>";
            spec.Fields["Custom.RESTAPIReviews"] = existingLink;
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(spec);
            _connection.RevisionConflictOnUpdate = failedPhase;

            var error = Assert.ThrowsAsync<Exception>(() => _devOpsService.UpdateSpecPullRequestAsync(
                100, NewSpecPullRequest, SpecCommit, PreviousSpecCommit, SpecApiVersion, CancellationToken.None));

            Assert.That(error!.Message, Does.Contain("Revision test failed"));
            Assert.That(_connection.CapturedPatches.Select(p => p.WorkItemId), Is.EqualTo(new[] { 100, 200, 100 }.Take(failedPhase + 1)),
                "A conflict must stop this update without a retry, rollback, or further publication.");
            Assert.That(_connection.UpdatedWorkItems, Has.Count.EqualTo(failedPhase));
            AssertRevisionGuard(_connection.CapturedPatches[failedPhase].Document, failedPhase == 2 ? 2 : 1);
            var storedPlan = _connection.GetStoredWorkItem(100);
            Assert.That(storedPlan.Fields[ReleasePlanWorkItem.SpecCommitSHAField], Is.EqualTo(failedPhase == 0 ? PreviousSpecCommit : string.Empty),
                "Only a failed initial clear retains the untouched old target. Later failures must leave the parent unpinned.");
            Assert.That(storedPlan.Fields["Custom.GenerationStatusForJava"], Is.EqualTo("Completed"));
            Assert.That(storedPlan.Fields["Custom.SDKGenerationPipelineForJava"], Is.EqualTo("existing-pipeline"));
            var storedSpec = _connection.GetStoredWorkItem(200);
            Assert.That(storedSpec.Fields["Custom.ActiveSpecPullRequestUrl"], Is.EqualTo(failedPhase == 2 ? NewSpecPullRequest : SpecPullRequest));
            Assert.That(storedSpec.Fields["Custom.APISpecversion"], Is.EqualTo(failedPhase == 2 ? SpecApiVersion : string.Empty));
            Assert.That(storedSpec.Fields["Custom.RESTAPIReviews"], Is.EqualTo(failedPhase == 2
                ? $"{existingLink}<br><a href=\"{NewSpecPullRequest}\">{NewSpecPullRequest}</a>"
                : existingLink));
            Assert.That(storedSpec.Fields.ContainsKey(ReleasePlanWorkItem.SpecCommitSHAField), Is.False);
        }

        [Test]
        public void UpdateSpecPullRequestAsync_RejectsDifferentChildApiVersionBeforeAnyWrite()
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = PreviousSpecCommit;
            var spec = CreateApiSpecWorkItemWithVersion(200, SpecPullRequest, "Active", "2025-01-01");
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(spec);

            var error = Assert.ThrowsAsync<Exception>(() => _devOpsService.UpdateSpecPullRequestAsync(
                100, NewSpecPullRequest, SpecCommit, PreviousSpecCommit, SpecApiVersion, CancellationToken.None));

            Assert.That(error!.Message, Does.Contain("separate release plan for a different API version"));
            Assert.That(_connection.CapturedPatches, Is.Empty);
            Assert.That(_connection.GetStoredWorkItem(100).Fields, Is.EquivalentTo(plan.Fields));
            Assert.That(_connection.GetStoredWorkItem(200).Fields, Is.EquivalentTo(spec.Fields));
        }

        [TestCase(null, 1)]
        [TestCase(0, 1)]
        [TestCase(1, null)]
        [TestCase(1, 0)]
        public void UpdateSpecPullRequestAsync_RejectsMissingParentOrChildRevisionBeforeAnyWrite(int? parentRevision, int? childRevision)
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Rev = parentRevision;
            plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = PreviousSpecCommit;
            var spec = CreateApiSpecWorkItem(200, SpecPullRequest, "Active");
            spec.Rev = childRevision;
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(spec);

            var error = Assert.ThrowsAsync<Exception>(() => _devOpsService.UpdateSpecPullRequestAsync(
                100, NewSpecPullRequest, SpecCommit, PreviousSpecCommit, SpecApiVersion, CancellationToken.None));

            Assert.That(error!.Message, Does.Contain("valid release plan and API Spec work item revisions"));
            Assert.That(_connection.CapturedPatches, Is.Empty);
            Assert.That(_connection.GetStoredWorkItem(100).Fields, Is.EquivalentTo(plan.Fields));
        }

        [TestCase(null)]
        [TestCase("")]
        public async Task UpdateSpecPullRequestAsync_ExplicitlyConfiguresAnUnpinnedTarget(string? previousPin)
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Fields["Custom.GenerationStatusForJava"] = "Pending";
            if (previousPin != null)
            {
                plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = previousPin;
            }
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(CreateApiSpecWorkItem(200, SpecPullRequest, "Active"));

            Assert.That(await _devOpsService.UpdateSpecPullRequestAsync(
                100, SpecPullRequest, SpecCommit, "", SpecApiVersion, CancellationToken.None), Is.True);

            Assert.That(_connection.CapturedPatches.Select(p => p.WorkItemId), Is.EqualTo(new[] { 100, 200, 100 }));
            Assert.That(_connection.GetStoredWorkItem(100).Fields[ReleasePlanWorkItem.SpecCommitSHAField], Is.EqualTo(SpecCommit));
            Assert.That(_connection.GetStoredWorkItem(100).Fields["Custom.GenerationStatusForJava"], Is.EqualTo("Pending"));
            Assert.That(_connection.GetStoredWorkItem(200).Fields["Custom.APISpecversion"], Is.EqualTo(SpecApiVersion));
            Assert.That(_connection.GetStoredWorkItem(200).Fields.ContainsKey(ReleasePlanWorkItem.SpecCommitSHAField), Is.False);
        }

        [TestCase("")]
        [TestCase(PreviousSpecCommit)]
        public async Task UpdateSpecPullRequestAsync_PrivatePreviewLinkWithoutShaLeavesParentUnpinned(string previousPin)
        {
            const string privatePr = "https://github.com/Azure/azure-rest-api-specs-pr/pull/456";
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Fields["Custom.ReleasePlanType"] = "Private Preview";
            plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = previousPin;
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(CreateApiSpecWorkItemWithVersion(200, SpecPullRequest, "Active", SpecApiVersion));

            Assert.That(await _devOpsService.UpdateSpecPullRequestAsync(
                100, privatePr, "", previousPin, "", CancellationToken.None), Is.True);

            Assert.That(_connection.CapturedPatches.Select(p => p.WorkItemId), Is.EqualTo(new[] { 100, 200, 100 }));
            Assert.That(_connection.CapturedPatches[0].Document.Single(p => p.Path == $"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}").Value, Is.EqualTo(string.Empty));
            Assert.That(_connection.CapturedPatches[2].Document.Single(p => p.Path == $"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}").Value, Is.EqualTo(string.Empty));
            Assert.That(_connection.CapturedPatches[1].Document.Any(p => p.Path == "/fields/Custom.APISpecversion"), Is.False,
                "Linking without a confirmed version must not overwrite the child's existing API version.");
            Assert.That(_connection.GetStoredWorkItem(100).Fields[ReleasePlanWorkItem.SpecCommitSHAField], Is.EqualTo(string.Empty));
            var storedSpec = _connection.GetStoredWorkItem(200);
            Assert.That(storedSpec.Fields["Custom.ActiveSpecPullRequestUrl"], Is.EqualTo(privatePr));
            Assert.That(storedSpec.Fields["Custom.APISpecversion"], Is.EqualTo(SpecApiVersion));
            Assert.That(storedSpec.Fields.ContainsKey(ReleasePlanWorkItem.SpecCommitSHAField), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(SpecCommit)]
        public async Task GetReleasePlanForWorkItemAsync_ReadsParentPinAndChildTargetWithoutBackfilling(string? commitSha)
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Fields["Custom.APISpecversion"] = "not-the-child-version";
            plan.Fields["Custom.ActiveSpecPullRequestUrl"] = "not-the-child-pr";
            if (commitSha != null)
            {
                plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = commitSha;
            }
            var spec = CreateApiSpecWorkItemWithVersion(200, SpecPullRequest, "Active", SpecApiVersion);
            // A stray legacy child field must neither override nor backfill the parent's pin.
            spec.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = PreviousSpecCommit;
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(spec);

            var result = await _devOpsService.GetReleasePlanForWorkItemAsync(100, CancellationToken.None);

            Assert.That(result.SpecCommitSHA, Is.EqualTo(commitSha ?? ""));
            Assert.That(result.SpecAPIVersion, Is.EqualTo(SpecApiVersion));
            Assert.That(result.ActiveSpecPullRequest, Is.EqualTo(SpecPullRequest));
            Assert.That(_connection.CapturedPatches, Is.Empty);
        }

        [TestCase("", "")]
        [TestCase(PreviousSpecCommit, "")]
        [TestCase("0123456789ABCDEF0123456789ABCDEF01234567", SpecCommit)]
        public async Task GetReleasePlanForWorkItemAsync_OnlyReturnsPinWhenParentStillMatchesAfterChildRead(string latestPin, string expectedPin)
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = SpecCommit;
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(CreateApiSpecWorkItemWithVersion(200, SpecPullRequest, "Active", SpecApiVersion));
            _connection.BeforeRead = (readNumber, workItem) =>
            {
                if (workItem.Id == 200)
                {
                    // Another writer changes the parent while this reader is fetching the child.
                    var updatedParent = _connection.GetStoredWorkItem(100);
                    updatedParent.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = latestPin;
                    updatedParent.Rev++;
                    _connection.AddWorkItem(updatedParent);
                }
            };

            var result = await _devOpsService.GetReleasePlanForWorkItemAsync(100, CancellationToken.None);

            Assert.That(result.SpecCommitSHA, Is.EqualTo(expectedPin), "Never mix a stale parent pin with a separately read child target.");
            Assert.That(result.SpecAPIVersion, Is.EqualTo(SpecApiVersion));
            Assert.That(result.ActiveSpecPullRequest, Is.EqualTo(SpecPullRequest));
            Assert.That(_connection.CapturedPatches, Is.Empty);
        }

        [TestCase(100, 2)]
        [TestCase(200, 1)]
        [TestCase(100, 3)]
        public async Task GetReleasePlanForWorkItemAsync_ReadFailureClearsResponsePin(int failedWorkItemId, int failedReadNumber)
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = SpecCommit;
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(CreateApiSpecWorkItemWithVersion(200, SpecPullRequest, "Active", SpecApiVersion));
            _connection.BeforeRead = (readNumber, workItem) =>
            {
                if (workItem.Id == failedWorkItemId && readNumber == failedReadNumber)
                {
                    throw new InvalidOperationException("Simulated work item read failure.");
                }
            };

            var result = await _devOpsService.GetReleasePlanForWorkItemAsync(100, CancellationToken.None);

            Assert.That(result.SpecCommitSHA, Is.Empty);
            Assert.That(_connection.GetStoredWorkItem(100).Fields[ReleasePlanWorkItem.SpecCommitSHAField], Is.EqualTo(SpecCommit),
                "A failed read clears only the response pin, not the saved work item.");
            Assert.That(_connection.CapturedPatches, Is.Empty);
        }

        [Test]
        public async Task GetReleasePlanForWorkItemAsync_MissingChildClearsResponsePin()
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = SpecCommit;
            _connection.AddWorkItem(plan);

            var result = await _devOpsService.GetReleasePlanForWorkItemAsync(100, CancellationToken.None);

            Assert.That(result.SpecCommitSHA, Is.Empty);
            Assert.That(result.SpecAPIVersion, Is.Empty);
            Assert.That(result.ActiveSpecPullRequest, Is.Empty);
            Assert.That(_connection.GetStoredWorkItem(100).Fields[ReleasePlanWorkItem.SpecCommitSHAField], Is.EqualTo(SpecCommit));
            Assert.That(_connection.CapturedPatches, Is.Empty);
        }

        [Test]
        public void ReleasePlanSpecCommit_IsWrittenOnlyOnParent()
        {
            var plan = new ReleasePlanWorkItem
            {
                SpecCommitSHA = SpecCommit,
                SpecAPIVersion = SpecApiVersion,
                ActiveSpecPullRequest = SpecPullRequest,
                SpecPullRequests = [SpecPullRequest]
            };

            Assert.That(ReleasePlanWorkItem.SpecCommitSHAField, Is.EqualTo("Custom.SpecCommitSHA"));
            Assert.That(plan.GetPatchDocument().Single(p => p.Path == $"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}").Value, Is.EqualTo(SpecCommit));
            var spec = plan.ToApiSpecWorkItem();
            Assert.That(spec.SpecAPIVersion, Is.EqualTo(SpecApiVersion));
            Assert.That(spec.ActiveSpecPullRequest, Is.EqualTo(SpecPullRequest));
            Assert.That(spec.GetPatchDocument().Any(p => p.Path.Equals($"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(System.Text.Json.JsonSerializer.Serialize(plan), Does.Contain($"\"SpecCommitSHA\":\"{SpecCommit}\""));
        }

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

            var result = await _devOpsService.UpdateSpecPullRequestAsync(100, newSpec, "", "", "", CancellationToken.None);

            Assert.That(result, Is.True);
            Assert.That(_connection.CapturedPatches.Select(p => p.WorkItemId), Is.EqualTo(new[] { 100, 200, 100 }));
            var specPatch = _connection.CapturedPatches.Single(p => p.WorkItemId == 200).Document;
            Assert.That(specPatch.Single(op => op.Path == "/fields/Custom.ActiveSpecPullRequestUrl").Value, Is.EqualTo(newSpec));
            Assert.That(specPatch.Single(op => op.Path == "/fields/Custom.RESTAPIReviews").Value,
                Is.EqualTo($"<a href=\"{oldSpec}\">{oldSpec}</a><br><a href=\"{newSpec}\">{newSpec}</a>"));
            var statusPatch = _connection.CapturedPatches[2].Document;
            AssertRevisionGuard(statusPatch, 2);
            Assert.That(statusPatch, Has.Count.EqualTo(languages.Length + 2));
            foreach (var language in languages)
            {
                Assert.That(statusPatch.Single(op => op.Path == $"/fields/Custom.GenerationStatusFor{language}").Value, Is.EqualTo("Not applicable"));
            }
            Assert.That(statusPatch.All(op => op.Path == "/rev" || op.Path == $"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}" ||
                op.Path.StartsWith("/fields/Custom.GenerationStatusFor", StringComparison.Ordinal)), Is.True,
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
                100, "https://github.com/Azure/azure-rest-api-specs/pull/456", "", "", "", CancellationToken.None);

            Assert.That(result, Is.True);
            Assert.That(_connection.CapturedPatches.Select(p => p.WorkItemId), Is.EqualTo(new[] { 100, 200, 100 }));
            var patch = _connection.CapturedPatches[2].Document;
            AssertRevisionGuard(patch, 2);
            Assert.That(patch, Has.Count.EqualTo(6));
            Assert.That(patch.Any(op => op.Path == "/fields/Custom.GenerationStatusForJava"), Is.False);
            Assert.That(patch.Skip(2).All(op => Equals(op.Value, "Not applicable")), Is.True);
            Assert.That(_connection.GetStoredWorkItem(100).Fields["Custom.GenerationStatusForJava"], Is.EqualTo(generationStatus));
        }

        [Test]
        public async Task UpdateSpecPullRequestAsync_AllLanguagesHaveRecordedRuns_PublishesPinWithoutResettingStatus()
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
                100, "https://github.com/Azure/azure-rest-api-specs/pull/456", SpecCommit, "", SpecApiVersion, CancellationToken.None);

            Assert.That(result, Is.True);
            Assert.That(_connection.CapturedPatches.Select(p => p.WorkItemId), Is.EqualTo(new[] { 100, 200, 100 }));
            var publishPatch = _connection.CapturedPatches[2].Document;
            AssertRevisionGuard(publishPatch, 2);
            Assert.That(publishPatch, Has.Count.EqualTo(2));
            Assert.That(publishPatch[1].Path, Is.EqualTo($"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}"));
            Assert.That(publishPatch[1].Value, Is.EqualTo(SpecCommit));
            var expectedFields = new Dictionary<string, object>(plan.Fields)
            {
                [ReleasePlanWorkItem.SpecCommitSHAField] = SpecCommit
            };
            Assert.That(_connection.GetStoredWorkItem(100).Fields, Is.EquivalentTo(expectedFields));
        }

        #endregion

        #region UpdateConfirmedReleaseTargetAsync Tests

        [TestCase(false)]
        [TestCase(true)]
        public async Task UpdateConfirmedReleaseTargetAsync_PublishesAllParentMetadataAndSdkDetailsWithPin(bool sameTarget)
        {
            var previousPin = sameTarget ? SpecCommit : PreviousSpecCommit;
            var target = CreateConfirmedReleaseTarget(sameTarget ? SpecPullRequest : NewSpecPullRequest);
            var fields = CreateConfirmedParentFields(target);
            var sdkInfos = CreateConfirmedSdkInfos(target);
            var plan = CreateReleasePlanForConfirmedUpdate(previousPin);
            var spec = CreateApiSpecWorkItemWithVersion(200, SpecPullRequest, "Active", ConfirmedApiVersion);
            spec.Rev = 7;
            spec.Fields["Custom.RESTAPIReviews"] = $"<a href=\"{SpecPullRequest}\">{SpecPullRequest}</a>";
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(spec);

            Assert.That(await _devOpsService.UpdateConfirmedReleaseTargetAsync(
                100, target, previousPin, fields, sdkInfos, CancellationToken.None), Is.True);

            Assert.That(_connection.CapturedPatches.Select(patch => patch.WorkItemId), Is.EqualTo(new[] { 100, 200, 100 }));
            AssertRevisionGuard(_connection.CapturedPatches[0].Document, 4);
            AssertRevisionGuard(_connection.CapturedPatches[1].Document, 7);
            AssertRevisionGuard(_connection.CapturedPatches[2].Document, 5);
            Assert.That(_connection.CapturedPatches[0].Document.Select(operation => operation.Path),
                Is.EqualTo(new[] { "/rev", $"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}" }));
            Assert.That(_connection.CapturedPatches[1].Document.Select(operation => operation.Path), Is.EquivalentTo(new[]
            {
                "/rev", "/fields/Custom.ActiveSpecPullRequestUrl", "/fields/Custom.RESTAPIReviews", "/fields/Custom.APISpecversion"
            }), "The child patch must not contain parent metadata or SDK package fields.");

            var clearedFields = new Dictionary<string, object>(plan.Fields)
            {
                [ReleasePlanWorkItem.SpecCommitSHAField] = string.Empty
            };
            Assert.That(_connection.UpdatedWorkItems, Has.Count.EqualTo(3));
            Assert.That(_connection.UpdatedWorkItems[0].Fields, Is.EquivalentTo(clearedFields),
                "No requested metadata or SDK details may be applied while clearing the old pin.");
            Assert.That(_connection.UpdatedWorkItems.Select(item => item.Rev), Is.EqualTo(new[] { 5, 8, 6 }));

            var publishPatch = _connection.CapturedPatches[2].Document;
            Assert.That(publishPatch.Single(operation => operation.Path == $"/fields/{ReleasePlanWorkItem.SpecCommitSHAField}").Value, Is.EqualTo(SpecCommit));
            foreach (var (field, value) in fields)
            {
                Assert.That(publishPatch.Single(operation => operation.Path == $"/fields/{field}").Value, Is.EqualTo(value));
            }
            var expectedPackages = new Dictionary<string, string>
            {
                ["Custom.PythonPackageName"] = "azure-mgmt-contoso",
                ["Custom.DotnetPackageName"] = "Azure.ResourceManager.Contoso",
                ["Custom.JavaPackageName"] = "com.azure.contoso",
                ["Custom.GoPackageName"] = "sdk/contoso/armcontoso"
            };
            foreach (var (field, value) in expectedPackages)
            {
                Assert.That(publishPatch.Single(operation => operation.Path == $"/fields/{field}").Value, Is.EqualTo(value));
            }
            Assert.That(publishPatch.Single(operation => operation.Path == "/fields/Custom.SDKLanguages").Value.ToString()!.Split(','),
                Is.EquivalentTo(new[] { "Python", ".NET", "Java", "Go", "JavaScript" }), "Retain previously tracked languages when adding the confirmed SDK packages.");
            Assert.That(publishPatch.Single(operation => operation.Path == "/fields/Custom.ReleaseExclusionStatusForPython").Value, Is.EqualTo("Not applicable"));
            Assert.That(publishPatch.Single(operation => operation.Path == "/fields/Custom.ReleaseExclusionStatusForDotnet").Value, Is.EqualTo("Not applicable"));
            Assert.That(publishPatch.Any(operation => operation.Path == "/fields/Custom.ReleaseExclusionStatusForJava" ||
                operation.Path == "/fields/Custom.ReleaseExclusionStatusForGo" || operation.Path == "/fields/Custom.JavaScriptPackageName"), Is.False,
                "Preserve intentional exclusions and packages not present in the confirmed metadata.");

            var storedPlan = _connection.GetStoredWorkItem(100);
            var expectedParent = new Dictionary<string, object>(plan.Fields)
            {
                [ReleasePlanWorkItem.SpecCommitSHAField] = SpecCommit,
                ["Custom.SDKLanguages"] = storedPlan.Fields["Custom.SDKLanguages"],
                ["Custom.ReleaseExclusionStatusForPython"] = "Not applicable",
                ["Custom.ReleaseExclusionStatusForDotnet"] = "Not applicable"
            };
            foreach (var (field, value) in fields.Concat(expectedPackages))
            {
                expectedParent[field] = value;
            }
            if (!sameTarget)
            {
                foreach (var language in new[] { "Dotnet", "JavaScript", "Python", "Go" })
                {
                    expectedParent[$"Custom.GenerationStatusFor{language}"] = "Not applicable";
                }
            }
            Assert.That(storedPlan.Fields, Is.EquivalentTo(expectedParent));
            Assert.That(_connection.UpdatedWorkItems[2].Fields, Is.EquivalentTo(expectedParent));
            var expectedChild = new Dictionary<string, object>(spec.Fields)
            {
                ["Custom.ActiveSpecPullRequestUrl"] = target.SpecPullRequestUrl,
                ["Custom.APISpecversion"] = ConfirmedApiVersion,
                ["Custom.RESTAPIReviews"] = sameTarget ? spec.Fields["Custom.RESTAPIReviews"]
                    : $"{spec.Fields["Custom.RESTAPIReviews"]}<br><a href=\"{NewSpecPullRequest}\">{NewSpecPullRequest}</a>"
            };
            Assert.That(_connection.GetStoredWorkItem(200).Fields, Is.EquivalentTo(expectedChild));
            Assert.That(_connection.UpdatedWorkItems[1].Fields, Is.EquivalentTo(expectedChild));
        }

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        public void UpdateConfirmedReleaseTargetAsync_RevisionConflictNeverAppliesRequestedParentFields(int failedPhase, bool sameTarget)
        {
            var previousPin = sameTarget ? SpecCommit : PreviousSpecCommit;
            var target = CreateConfirmedReleaseTarget(sameTarget ? SpecPullRequest : NewSpecPullRequest);
            var fields = CreateConfirmedParentFields(target);
            var plan = CreateReleasePlanForConfirmedUpdate(previousPin);
            var spec = CreateApiSpecWorkItemWithVersion(200, SpecPullRequest, "Active", "");
            spec.Rev = 7;
            spec.Fields["Custom.RESTAPIReviews"] = $"<a href=\"{SpecPullRequest}\">{SpecPullRequest}</a>";
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(spec);
            _connection.RevisionConflictOnUpdate = failedPhase;

            var error = Assert.ThrowsAsync<Exception>(() => _devOpsService.UpdateConfirmedReleaseTargetAsync(
                100, target, previousPin, fields, CreateConfirmedSdkInfos(target), CancellationToken.None));

            Assert.That(error!.Message, Does.Contain("Revision test failed"));
            Assert.That(_connection.CapturedPatches.Select(patch => patch.WorkItemId), Is.EqualTo(new[] { 100, 200, 100 }.Take(failedPhase + 1)),
                "Stop at the first conflict without retrying, restoring the pin, or separately writing metadata.");
            Assert.That(_connection.UpdatedWorkItems, Has.Count.EqualTo(failedPhase));
            var expectedRevisions = new[] { 4, 7, 5 };
            for (var phase = 0; phase <= failedPhase; phase++)
            {
                AssertRevisionGuard(_connection.CapturedPatches[phase].Document, expectedRevisions[phase]);
            }
            var expectedParent = new Dictionary<string, object>(plan.Fields)
            {
                [ReleasePlanWorkItem.SpecCommitSHAField] = failedPhase == 0 ? previousPin : string.Empty
            };
            Assert.That(_connection.GetStoredWorkItem(100).Fields, Is.EquivalentTo(expectedParent),
                "SDK release type, package names, exclusions, and every requested metadata field must remain unchanged.");
            foreach (var savedParent in _connection.UpdatedWorkItems.Where(item => item.Id == 100))
            {
                Assert.That(savedParent.Fields, Is.EquivalentTo(expectedParent));
            }
            var expectedChild = new Dictionary<string, object>(spec.Fields);
            if (failedPhase == 2)
            {
                expectedChild["Custom.ActiveSpecPullRequestUrl"] = target.SpecPullRequestUrl;
                expectedChild["Custom.APISpecversion"] = ConfirmedApiVersion;
                if (!sameTarget)
                {
                    expectedChild["Custom.RESTAPIReviews"] = $"{spec.Fields["Custom.RESTAPIReviews"]}<br><a href=\"{NewSpecPullRequest}\">{NewSpecPullRequest}</a>";
                }
                var attemptedPublication = _connection.CapturedPatches[2].Document;
                Assert.That(attemptedPublication.Single(operation => operation.Path == "/fields/Custom.SDKtypetobereleased").Value, Is.EqualTo("stable"));
                Assert.That(attemptedPublication.Single(operation => operation.Path == "/fields/Custom.PythonPackageName").Value, Is.EqualTo("azure-mgmt-contoso"));
            }
            else
            {
                Assert.That(_connection.CapturedPatches.SelectMany(patch => patch.Document).Any(operation =>
                    fields.ContainsKey(operation.Path.Replace("/fields/", string.Empty, StringComparison.Ordinal)) ||
                    operation.Path.EndsWith("PackageName", StringComparison.Ordinal) ||
                    operation.Path.StartsWith("/fields/Custom.ReleaseExclusionStatusFor", StringComparison.Ordinal)), Is.False);
            }
            Assert.That(_connection.GetStoredWorkItem(200).Fields, Is.EquivalentTo(expectedChild));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UpdateConfirmedReleaseTargetAsync_PinChangesDuringChildRead_DoesNotWriteMetadata(bool sameTarget)
        {
            var previousPin = sameTarget ? SpecCommit : PreviousSpecCommit;
            var changedPin = new string('b', 40);
            var target = CreateConfirmedReleaseTarget(sameTarget ? SpecPullRequest : NewSpecPullRequest);
            var plan = CreateReleasePlanForConfirmedUpdate(previousPin);
            var spec = CreateApiSpecWorkItemWithVersion(200, SpecPullRequest, "Active", ConfirmedApiVersion);
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(spec);
            _connection.BeforeRead = (_, workItem) =>
            {
                if (workItem.Id == 200)
                {
                    var concurrentParent = _connection.GetStoredWorkItem(100);
                    concurrentParent.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = changedPin;
                    concurrentParent.Rev++;
                    _connection.AddWorkItem(concurrentParent);
                }
            };

            var error = Assert.ThrowsAsync<Exception>(() => _devOpsService.UpdateConfirmedReleaseTargetAsync(
                100, target, previousPin, CreateConfirmedParentFields(target), CreateConfirmedSdkInfos(target), CancellationToken.None));

            Assert.That(error!.Message, Does.Contain("spec commit changed"));
            Assert.That(_connection.CapturedPatches, Is.Empty);
            Assert.That(_connection.UpdatedWorkItems, Is.Empty);
            var expectedParent = new Dictionary<string, object>(plan.Fields)
            {
                [ReleasePlanWorkItem.SpecCommitSHAField] = changedPin
            };
            Assert.That(_connection.GetStoredWorkItem(100).Fields, Is.EquivalentTo(expectedParent));
            Assert.That(_connection.GetStoredWorkItem(200).Fields, Is.EquivalentTo(spec.Fields));
        }

        [TestCase("beta", "stable")]
        [TestCase("stable", "beta")]
        public async Task UpdateReleasePlan_SameShaPinChangesDuringMetadataValidation_RejectsAllParentWrites(string storedType, string requestedType)
        {
            using var cancellation = new CancellationTokenSource();
            var ct = cancellation.Token;
            const string projectPath = "TypeSpecTestData/specification/testcontoso/Contoso.Management";
            var changedPin = new string('b', 40);
            var target = CreateConfirmedReleaseTarget(SpecPullRequest);
            target.SDKReleaseType = requestedType;
            var plan = CreateReleasePlanForConfirmedUpdate(SpecCommit);
            plan.Fields["Custom.SDKtypetobereleased"] = storedType;
            var spec = CreateApiSpecWorkItemWithVersion(200, SpecPullRequest, "Active", ConfirmedApiVersion);
            _connection.AddWorkItem(plan);
            _connection.AddWorkItem(spec);

            ReleasePlanWorkItem? observedPlan = null;
            var devops = new Mock<IDevOpsService>(MockBehavior.Strict);
            devops.Setup(service => service.ResolveReleasePlanByIdAsync(100, ct)).Returns(async () =>
            {
                observedPlan = await _devOpsService.GetReleasePlanForWorkItemAsync(100, ct);
                return observedPlan;
            });
            devops.Setup(service => service.UpdateConfirmedReleaseTargetAsync(100, It.IsAny<ReleasePlanSpecTarget>(), SpecCommit,
                    It.IsAny<Dictionary<string, string>>(), It.IsAny<List<SDKInfo>>(), ct))
                .Returns((int id, ReleasePlanSpecTarget proposed, string expectedPin, Dictionary<string, string> fields, List<SDKInfo> sdkInfos, CancellationToken token) =>
                    _devOpsService.UpdateConfirmedReleaseTargetAsync(id, proposed, expectedPin, fields, sdkInfos, token));

            var metadata = TypeSpecProject.ParseTypeSpecConfig(projectPath);
            metadata.AvailableApiVersions = [ConfirmedApiVersion];
            metadata.Packages = target.Packages;
            var typeSpec = new Mock<ITypeSpecHelper>(MockBehavior.Strict);
            typeSpec.Setup(helper => helper.IsUrl(projectPath)).Returns(false);
            typeSpec.Setup(helper => helper.GetTypeSpecProjectRelativePath(projectPath)).Returns(ConfirmedProjectPath);
            typeSpec.Setup(helper => helper.IsTypeSpecProjectForMgmtPlane(projectPath)).Returns(true);
            typeSpec.Setup(helper => helper.ValidateReleasePlanSnapshotAsync(projectPath, SpecCommit, It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), ct))
                .Callback(() =>
                {
                    // The resolved tool snapshot still has A, but another writer saves B while compilation runs.
                    var concurrentParent = _connection.GetStoredWorkItem(100);
                    concurrentParent.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = changedPin;
                    concurrentParent.Rev++;
                    _connection.AddWorkItem(concurrentParent);
                })
                .ReturnsAsync(metadata);
            var npx = new Mock<INpxHelper>(MockBehavior.Strict);
            var github = new MockGitHubService { ConfiguredHeadSha = SpecCommit };
            using var httpClient = new HttpClient(Mock.Of<HttpMessageHandler>());
            var tool = new ReleasePlanTool(devops.Object, Mock.Of<IGitHelper>(), typeSpec.Object, new TestLogger<ReleasePlanTool>(),
                Mock.Of<IUserHelper>(), github, Mock.Of<IEnvironmentHelper>(), new InputSanitizer(), httpClient,
                npx.Object, Mock.Of<IRawOutputHelper>(), Mock.Of<INotificationService>());

            var response = await tool.UpdateReleasePlan(projectPath, SpecPullRequest, requestedType, workItemId: 100,
                serviceTreeId: "11111111-1111-1111-1111-111111111111", apiVersion: ConfirmedApiVersion,
                specCommitSha: SpecCommit, confirmTarget: true, expectedSpecCommitSha: SpecCommit, ct: ct);

            Assert.That(response.ResponseError, Does.Contain("spec commit changed"));
            Assert.That(response.ReleasePlanDetails, Is.Null);
            Assert.That(observedPlan, Is.Not.Null);
            Assert.That(observedPlan!.SpecCommitSHA, Is.EqualTo(SpecCommit), "The resolved plan must be a snapshot, not a reference to mutable storage.");
            Assert.That(_connection.CapturedPatches, Is.Empty, "Even an unchanged proposed SHA must be checked before SDK type or package metadata is written.");
            Assert.That(_connection.UpdatedWorkItems, Is.Empty);
            var expectedParent = new Dictionary<string, object>(plan.Fields)
            {
                [ReleasePlanWorkItem.SpecCommitSHAField] = changedPin
            };
            Assert.That(_connection.GetStoredWorkItem(100).Fields, Is.EquivalentTo(expectedParent));
            Assert.That(_connection.GetStoredWorkItem(200).Fields, Is.EquivalentTo(spec.Fields));
            devops.Verify(service => service.UpdateConfirmedReleaseTargetAsync(100,
                It.Is<ReleasePlanSpecTarget>(proposed => proposed.SpecCommitSHA == SpecCommit && proposed.ApiVersion == ConfirmedApiVersion &&
                    proposed.SpecPullRequestUrl == SpecPullRequest && proposed.TypeSpecProjectPath == ConfirmedProjectPath &&
                    proposed.SDKReleaseType == requestedType && proposed.ExpectedPreviousSpecCommitSHA == SpecCommit), SpecCommit,
                It.Is<Dictionary<string, string>>(fields => fields.Count == 3 && fields["Custom.SDKtypetobereleased"] == requestedType &&
                    fields["Custom.ApiSpecProjectPath"] == ConfirmedProjectPath && fields["Custom.ServiceTreeID"] == "11111111-1111-1111-1111-111111111111"),
                It.Is<List<SDKInfo>>(sdkInfos => sdkInfos.Count == 4 && sdkInfos.Any(sdk => sdk.Language == "Python" && sdk.PackageName == "azure-mgmt-contoso") &&
                    sdkInfos.Any(sdk => sdk.Language == ".NET" && sdk.PackageName == "Azure.ResourceManager.Contoso") &&
                    sdkInfos.Any(sdk => sdk.Language == "Java" && sdk.PackageName == "com.azure.contoso") &&
                    sdkInfos.Any(sdk => sdk.Language == "Go" && sdk.PackageName == "sdk/contoso/armcontoso")), ct), Times.Once);
            devops.Verify(service => service.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
            devops.Verify(service => service.UpdateWorkItemAsync(It.IsAny<int>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
            devops.Verify(service => service.UpdateReleasePlanSDKDetailsAsync(It.IsAny<int>(), It.IsAny<List<SDKInfo>>(), It.IsAny<CancellationToken>()), Times.Never);
            devops.Verify(service => service.UpdateSpecPullRequestAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            devops.Verify(service => service.UpdateApiSpecVersionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            typeSpec.Verify(helper => helper.ValidateReleasePlanSnapshotAsync(projectPath, SpecCommit, It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), ct), Times.Once);
            typeSpec.Verify(helper => helper.ParseTypeSpecProjectAsync(It.IsAny<string>(), It.IsAny<INpxHelper>(), It.IsAny<ILogger>(), It.IsAny<CancellationToken>()), Times.Never);
            npx.VerifyNoOtherCalls();
        }

        #endregion

        #region Helper Methods

        private static ReleasePlanSpecTarget CreateConfirmedReleaseTarget(string pullRequestUrl) => new()
        {
            TypeSpecProjectPath = ConfirmedProjectPath,
            ApiVersion = ConfirmedApiVersion,
            SpecCommitSHA = SpecCommit,
            SpecPullRequestUrl = pullRequestUrl,
            SDKReleaseType = "stable",
            Packages =
            [
                new PackageInfo { Language = SdkLanguage.Python, PackageName = "azure-mgmt-contoso", ApiVersion = ConfirmedApiVersion },
                new PackageInfo { Language = SdkLanguage.DotNet, PackageName = "Azure.ResourceManager.Contoso", ApiVersion = ConfirmedApiVersion },
                new PackageInfo { Language = SdkLanguage.Java, PackageName = "com.azure.contoso", ApiVersion = ConfirmedApiVersion },
                new PackageInfo { Language = SdkLanguage.Go, PackageName = "sdk/contoso/armcontoso", ApiVersion = ConfirmedApiVersion }
            ]
        };

        private static Dictionary<string, string> CreateConfirmedParentFields(ReleasePlanSpecTarget target) => new()
        {
            ["Custom.SDKtypetobereleased"] = target.SDKReleaseType,
            ["Custom.ApiSpecProjectPath"] = target.TypeSpecProjectPath,
            ["Custom.ServiceTreeID"] = "11111111-1111-1111-1111-111111111111",
            ["Custom.ProductServiceTreeID"] = "22222222-2222-2222-2222-222222222222",
            ["Custom.ProductName"] = "Contoso Product",
            ["Custom.ProductLifecycle"] = "GA",
            ["Custom.ProductType"] = "Offering"
        };

        private static List<SDKInfo> CreateConfirmedSdkInfos(ReleasePlanSpecTarget target) =>
            target.Packages.Select(package => new SDKInfo { Language = package.Language.ToWorkItemString(), PackageName = package.PackageName! }).ToList();

        private WorkItem CreateReleasePlanForConfirmedUpdate(string commitSha)
        {
            var plan = CreateReleasePlanWorkItemWithApiSpecChild(100, "In Progress", 200);
            plan.Rev = 4;
            plan.Fields[ReleasePlanWorkItem.SpecCommitSHAField] = commitSha;
            plan.Fields["Custom.ReleasePlanType"] = "GA";
            plan.Fields["Custom.MgmtScope"] = "Yes";
            plan.Fields["Custom.SDKtypetobereleased"] = "beta";
            plan.Fields["Custom.ApiSpecProjectPath"] = ConfirmedProjectPath;
            plan.Fields["Custom.ServiceTreeID"] = "33333333-3333-3333-3333-333333333333";
            plan.Fields["Custom.ProductServiceTreeID"] = "44444444-4444-4444-4444-444444444444";
            plan.Fields["Custom.ProductName"] = "Existing product";
            plan.Fields["Custom.ProductLifecycle"] = "Public Preview";
            plan.Fields["Custom.ProductType"] = "Feature";
            plan.Fields["Custom.SDKLanguages"] = "Python,Java,JavaScript,Go";
            plan.Fields["Custom.PythonPackageName"] = "azure-mgmt-existing";
            plan.Fields["Custom.JavaPackageName"] = "com.azure.existing";
            plan.Fields["Custom.GoPackageName"] = "sdk/existing/armexisting";
            plan.Fields["Custom.JavaScriptPackageName"] = "@azure/arm-existing";
            plan.Fields["Custom.ReleaseExclusionStatusForPython"] = "MissingEmitterConfig";
            plan.Fields["Custom.ReleaseExclusionStatusForDotnet"] = "missingemitterconfig";
            plan.Fields["Custom.ReleaseExclusionStatusForJava"] = "Requested";
            plan.Fields["Custom.ReleaseExclusionStatusForGo"] = "Approved";
            plan.Fields["Custom.GenerationStatusForJava"] = "In progress";
            plan.Fields["Custom.SDKGenerationPipelineForJava"] = "existing-pipeline";
            return plan;
        }

        private static void AssertRevisionGuard(DevOpsJsonPatchDocument document, int revision)
        {
            Assert.That(document, Is.Not.Empty);
            Assert.That(document[0].Operation, Is.EqualTo(DevOpsPatchOperation.Test));
            Assert.That(document[0].Path, Is.EqualTo("/rev"));
            Assert.That(document[0].Value, Is.EqualTo(revision));
        }

        private WorkItem CreateReleasePlanWorkItemWithReleasePlanId(int workItemId, int releasePlanId, string state)
        {
            return new WorkItem
            {
                Id = workItemId,
                Rev = 1,
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
                Rev = 1,
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

        [TestCase(null)]
        [TestCase("")]
        [TestCase("main")]
        [TestCase("refs/pull/123/merge")]
        [TestCase("abc123")]
        [TestCase("gggggggggggggggggggggggggggggggggggggggg")]
        public void RunSDKGenerationPipelineAsync_RejectsMutableOrInvalidSource(string? source)
        {
            Assert.ThrowsAsync<ArgumentException>(() => _devOpsService.RunSDKGenerationPipelineAsync(
                source!, "specification/test/service", "2026-01-01-preview", "beta", "Java", 0));
        }

        [Test, Combinatorial]
        [NonParallelizable]
        public async Task RunSDKGenerationPipelineAsync_QueuesPinnedSourceVersionWithExplicitPolicyRegardlessOfEnvironment(
            [Values("Java", "Python", ".NET", "JavaScript", "Go")] string language,
            [Values(false, true)] bool autoRelease,
            [Values(false, true)] bool inPipeline)
        {
            var definitionId = language switch
            {
                "Java" => 7421,
                "Python" => 7423,
                ".NET" => 7412,
                "JavaScript" => 7422,
                "Go" => 7426,
                _ => throw new ArgumentOutOfRangeException(nameof(language))
            };
            using var cancellation = new CancellationTokenSource();
            var ct = cancellation.Token;
            var buildClient = new Mock<BuildHttpClient>(new Uri("https://dev.azure.com/test"), new Microsoft.VisualStudio.Services.Common.VssCredentials());
            var projectClient = new Mock<ProjectHttpClient>(new Uri("https://dev.azure.com/test"), new Microsoft.VisualStudio.Services.Common.VssCredentials());
            var connection = new Mock<IDevOpsConnection>();
            connection.Setup(x => x.GetBuildClient(ct)).Returns(buildClient.Object);
            connection.Setup(x => x.GetProjectClient(ct)).Returns(projectClient.Object);
            buildClient.Setup(x => x.GetDefinitionAsync("internal", definitionId, null, null, null, null, null, ct))
                .ReturnsAsync(new BuildDefinition { Id = definitionId, Name = "SDK generation" });
            projectClient.Setup(x => x.GetProject("internal", null, false, null))
                .ReturnsAsync(new TeamProject { Id = Guid.NewGuid(), Name = "internal" });
            Build? queuedBuild = null;
            buildClient.Setup(x => x.QueueBuildAsync(It.IsAny<Build>(), null, null, null, null, null, ct))
                .Callback(new InvocationAction(invocation => queuedBuild = (Build)invocation.Arguments[0]))
                .ReturnsAsync(new Build { Id = 99 });
            var service = new DevOpsService(_logger, connection.Object);
            var originalTeamProject = Environment.GetEnvironmentVariable("SYSTEM_TEAMPROJECTID");
            try
            {
                Environment.SetEnvironmentVariable("SYSTEM_TEAMPROJECTID", inPipeline ? "test-project" : null);

                await service.RunSDKGenerationPipelineAsync(
                    SpecCommit, "specification/test/service", "2026-01-01-preview", "beta", language, 0,
                    sdkRepoBranch: "feature/existing-sdk", autoRelease: autoRelease, ct: ct);

                Assert.That(queuedBuild, Is.Not.Null);
                Assert.That(queuedBuild!.SourceBranch, Is.EqualTo("main"), "A commit SHA belongs in SourceVersion, not SourceBranch.");
                Assert.That(queuedBuild.SourceVersion, Is.EqualTo(SpecCommit));
                Assert.That(queuedBuild.Definition.Id, Is.EqualTo(definitionId));
                Assert.That(queuedBuild.TemplateParameters["TriggerSource"], Is.EqualTo(autoRelease ? "sdk-release" : "sdk-review"),
                    "Only the explicit release policy may enable auto-release; the host environment must not change it.");
                Assert.That(queuedBuild.TemplateParameters["ApiVersion"], Is.EqualTo("2026-01-01-preview"));
                Assert.That(queuedBuild.TemplateParameters["SdkRepoBranch"], Is.EqualTo("feature/existing-sdk"));
                Assert.That(queuedBuild.TemplateParameters["ConfigPath"], Is.EqualTo("specification/test/service/tspconfig.yaml"));
                Assert.That(queuedBuild.TemplateParameters.ContainsKey("SpecCommitSha"), Is.False, "The pipeline has no such template parameter.");
                Assert.That(queuedBuild.TemplateParameters["SdkReleaseType"], Is.EqualTo("beta"),
                    "Automation must forward the confirmed release type just like an interactive invocation.");
                Assert.That(queuedBuild.TemplateParameters["CreatePullRequest"], Is.EqualTo("true"));
                Assert.That(queuedBuild.TemplateParameters.Keys, Is.EquivalentTo(new[]
                {
                    "ConfigType", "ConfigPath", "CreatePullRequest", "ReleasePlanWorkItemId", "TriggerSource",
                    "SdkReleaseType", "ApiVersion", "SdkRepoBranch"
                }), "Release policy must use the existing TriggerSource parameter, not a new template parameter.");
                buildClient.Verify(x => x.QueueBuildAsync(It.IsAny<Build>(), null, null, null, null, null, ct), Times.Once);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SYSTEM_TEAMPROJECTID", originalTeamProject);
            }
        }

        [TestCase(false, "sdk-review")]
        [TestCase(true, "sdk-release")]
        public void BuildSdkGenerationTemplateParams_PreservesConfirmedTargetAndUsesExplicitReleasePolicy(bool autoRelease, string expectedTriggerSource)
        {
            // Arrange
            var method = typeof(DevOpsService).GetMethod("BuildSdkGenerationTemplateParams", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            // Act
            var templateParams = (Dictionary<string, string>)method!.Invoke(null, ["specification/test/service", 0, "stable", "v1", "feature/sdk-branch", autoRelease])!;

            // Assert
            Assert.That(templateParams, Is.EquivalentTo(new Dictionary<string, string>
            {
                ["ConfigType"] = "TypeSpec",
                ["ConfigPath"] = "specification/test/service/tspconfig.yaml",
                ["CreatePullRequest"] = "true",
                ["ReleasePlanWorkItemId"] = "0",
                ["TriggerSource"] = expectedTriggerSource,
                ["SdkRepoBranch"] = "feature/sdk-branch",
                ["SdkReleaseType"] = "stable",
                ["ApiVersion"] = "v1"
            }));
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

            public List<(int WorkItemId, DevOpsJsonPatchDocument Document)> CapturedPatches => _workItemClient.CapturedPatches;

            public List<WorkItem> UpdatedWorkItems => _workItemClient.UpdatedWorkItems;

            public int? RevisionConflictOnUpdate
            {
                get => _workItemClient.RevisionConflictOnUpdate;
                set => _workItemClient.RevisionConflictOnUpdate = value;
            }

            public Action<int, WorkItem>? BeforeRead
            {
                get => _workItemClient.BeforeRead;
                set => _workItemClient.BeforeRead = value;
            }

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

            public WorkItem GetStoredWorkItem(int id)
            {
                return _workItemClient.GetStoredWorkItem(id);
            }

            public void CancelQuery()
            {
                _workItemClient.CancelQuery = true;
            }

            public void CancelBulkFetch()
            {
                _workItemClient.CancelBulkFetch = true;
            }
        }

        private class TestWorkItemClient : WorkItemTrackingHttpClient
        {
            private readonly List<WorkItem> _queryWorkItems = new();
            private readonly Dictionary<int, WorkItem> _workItems = new();
            private readonly Dictionary<int, int> _readCounts = new();

            public string? LastCapturedQuery { get; private set; }

            public Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument? LastCapturedPatchDocument { get; private set; }

            public List<(int WorkItemId, DevOpsJsonPatchDocument Document)> CapturedPatches { get; } = [];

            public List<WorkItem> UpdatedWorkItems { get; } = [];

            public bool CancelQuery { get; set; }

            public bool CancelBulkFetch { get; set; }

            // Zero-based patch attempt at which another writer advances the stored revision.
            public int? RevisionConflictOnUpdate { get; set; }

            // Invoked before each snapshot is returned, with the per-work-item read count.
            public Action<int, WorkItem>? BeforeRead { get; set; }

            public TestWorkItemClient() : base(new Uri("https://dev.azure.com/test"), null)
            {
            }

            public void AddWorkItemToQuery(WorkItem workItem)
            {
                _queryWorkItems.Add(workItem);
                AddWorkItem(workItem);
            }

            public void AddWorkItem(WorkItem workItem)
            {
                if (workItem.Id.HasValue)
                {
                    _workItems[workItem.Id.Value] = CreateSnapshot(workItem);
                }
            }

            public WorkItem GetStoredWorkItem(int id)
            {
                return CreateSnapshot(_workItems[id]);
            }

            private static WorkItem CreateSnapshot(WorkItem workItem)
            {
                return new WorkItem
                {
                    Id = workItem.Id,
                    Rev = workItem.Rev,
                    Url = workItem.Url,
                    Fields = workItem.Fields == null ? null : new Dictionary<string, object>(workItem.Fields),
                    Relations = workItem.Relations?.Select(relation => new WorkItemRelation
                    {
                        Rel = relation.Rel,
                        Url = relation.Url,
                        Attributes = relation.Attributes == null ? null : new Dictionary<string, object>(relation.Attributes)
                    }).ToList()
                };
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
                var workItems = _queryWorkItems.Where(wi => ids.Contains(wi.Id ?? 0))
                    .Select(wi => GetStoredWorkItem(wi.Id!.Value)).ToList();
                return Task.FromResult(workItems);
            }

            public override Task<WorkItem> GetWorkItemAsync(string project, int id, IEnumerable<string>? fields = null, DateTime? asOf = null, WorkItemExpand? expand = null, object? userState = null, CancellationToken cancellationToken = default(CancellationToken))
            {
                return GetWorkItemAsync(id, fields, asOf, expand, userState, cancellationToken);
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
                if (_workItems.TryGetValue(id, out var workItem))
                {
                    var readCount = _readCounts.GetValueOrDefault(id) + 1;
                    _readCounts[id] = readCount;
                    BeforeRead?.Invoke(readCount, workItem);
                    return Task.FromResult(CreateSnapshot(workItem));
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
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromCanceled<WorkItem>(cancellationToken);
                }
                LastCapturedPatchDocument = document;
                var updateIndex = CapturedPatches.Count;
                CapturedPatches.Add((id, document));
                if (!_workItems.TryGetValue(id, out var workItem))
                {
                    throw new InvalidOperationException($"Work item {id} not found");
                }

                if (RevisionConflictOnUpdate == updateIndex)
                {
                    workItem.Rev = (workItem.Rev ?? 0) + 1;
                }
                foreach (var operation in document.Where(op => op.Operation == DevOpsPatchOperation.Test))
                {
                    if (operation.Path != "/rev")
                    {
                        throw new NotSupportedException($"Unsupported test path {operation.Path}");
                    }
                    if (workItem.Rev != Convert.ToInt32(operation.Value))
                    {
                        throw new InvalidOperationException($"Revision test failed for work item {id}");
                    }
                }

                // Each individual work item patch is revision guarded; the two work items are not a transaction.
                var updatedWorkItem = CreateSnapshot(workItem);
                foreach (var operation in document.Where(op => op.Operation != DevOpsPatchOperation.Test))
                {
                    const string fieldPrefix = "/fields/";
                    if (!operation.Path.StartsWith(fieldPrefix, StringComparison.Ordinal))
                    {
                        throw new NotSupportedException($"Unsupported update path {operation.Path}");
                    }
                    var field = operation.Path[fieldPrefix.Length..];
                    switch (operation.Operation)
                    {
                        case DevOpsPatchOperation.Add:
                        case DevOpsPatchOperation.Replace:
                            updatedWorkItem.Fields[field] = operation.Value;
                            break;
                        case DevOpsPatchOperation.Remove:
                            updatedWorkItem.Fields.Remove(field);
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported patch operation {operation.Operation}");
                    }
                }
                updatedWorkItem.Rev = (workItem.Rev ?? 0) + 1;
                _workItems[id] = updatedWorkItem;
                UpdatedWorkItems.Add(CreateSnapshot(updatedWorkItem));
                return Task.FromResult(CreateSnapshot(updatedWorkItem));
            }

        }

        #endregion
    }
}
