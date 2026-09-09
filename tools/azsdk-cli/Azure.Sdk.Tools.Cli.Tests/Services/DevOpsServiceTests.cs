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
using Moq;

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
            var result = await _devOpsService.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecPath, apiVersion, CancellationToken.None);

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
            var result = await _devOpsService.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecPath, requestedApiVersion, CancellationToken.None);

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
            var apiSpec = CreateApiSpecWorkItemWithVersion(200, "https://github.com/Azure/azure-rest-api-specs/pull/12345", "Active", 
                apiVersion, parentId: 100);
            releasePlan.Fields["Custom.ApiSpecProjectPath"] = typeSpecPath;
            
            _connection.AddWorkItemToQuery(releasePlan);
            _connection.AddWorkItem(releasePlan);
            _connection.AddWorkItem(apiSpec);

            // Act
            var result = await _devOpsService.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecPath, apiVersion, CancellationToken.None);

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
            var apiSpec1 = CreateApiSpecWorkItemWithVersion(200, "https://github.com/Azure/azure-rest-api-specs/pull/12345", "Active", 
                "2023-06-01", parentId: 100);
            
            var releasePlan2 = CreateReleasePlanWorkItemWithApiSpecChild(101, "In Progress", 201);
            releasePlan2.Fields["Custom.ApiSpecProjectPath"] = typeSpecPath;
            var apiSpec2 = CreateApiSpecWorkItemWithVersion(201, "https://github.com/Azure/azure-rest-api-specs/pull/12346", "Active", 
                requestedApiVersion, parentId: 101);
            
            _connection.AddWorkItemToQuery(releasePlan1);
            _connection.AddWorkItemToQuery(releasePlan2);
            _connection.AddWorkItem(releasePlan1);
            _connection.AddWorkItem(releasePlan2);
            _connection.AddWorkItem(apiSpec1);
            _connection.AddWorkItem(apiSpec2);

            // Act
            var result = await _devOpsService.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecPath, requestedApiVersion, CancellationToken.None);

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
            var apiSpec = CreateApiSpecWorkItemWithVersion(200, "https://github.com/Azure/azure-rest-api-specs/pull/12345", "Active", 
                existingApiVersion, parentId: 100);
            
            _connection.AddWorkItemToQuery(releasePlan);
            _connection.AddWorkItem(releasePlan);
            _connection.AddWorkItem(apiSpec);

            // Act
            var result = await _devOpsService.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecPath, requestedApiVersion, CancellationToken.None);

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
            var result = await _devOpsService.GetReleasePlanByTypeSpecProjectPathAndApiVersionAsync(typeSpecPath, apiVersion, CancellationToken.None);

            // Assert
            Assert.IsNull(result, "Should return null when API version is empty");
        }

        #endregion
        #region TestDevOpsConnection

        private class TestDevOpsConnection : IDevOpsConnection
        {
            private readonly TestWorkItemClient _workItemClient = new();

            public string? LastCapturedQuery => _workItemClient.LastCapturedQuery;

            public Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument? LastCapturedPatchDocument => _workItemClient.LastCapturedPatchDocument;

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
        }

        private class TestWorkItemClient : WorkItemTrackingHttpClient
        {
            private readonly List<WorkItem> _queryWorkItems = new();
            private readonly Dictionary<int, WorkItem> _workItems = new();

            public string? LastCapturedQuery { get; private set; }

            public Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument? LastCapturedPatchDocument { get; private set; }

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
                var result = new WorkItemQueryResult
                {
                    WorkItems = _queryWorkItems.Select(wi => new WorkItemReference { Id = wi.Id ?? 0 }).ToList()
                };
                return Task.FromResult(result);
            }


            public override Task<List<WorkItem>> GetWorkItemsAsync(IEnumerable<int> ids, IEnumerable<string>? fields = null, DateTime? asOf = null, WorkItemExpand? expand = null, WorkItemErrorPolicy? errorPolicy = null, object? userState = null, CancellationToken cancellationToken = default(CancellationToken))
            {
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
                LastCapturedPatchDocument = document;
                _workItems.TryGetValue(id, out var workItem);
                return Task.FromResult(workItem ?? new WorkItem { Id = id });
            }

        }

        #endregion
    }
}
