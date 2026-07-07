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
        #region TestDevOpsConnection

        private class TestDevOpsConnection : IDevOpsConnection
        {
            private readonly TestWorkItemClient _workItemClient = new();

            public string? LastCapturedQuery => _workItemClient.LastCapturedQuery;

            public BuildHttpClient GetBuildClient(CancellationToken ct = default)
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

        }

        #endregion
    }
}
