// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;
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
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl);

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
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl);

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
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl);

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
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl);

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
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl);

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
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl);

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
            var result = await _devOpsService.GetReleasePlanAsync(pullRequestUrl);

            // Assert
            Assert.IsNull(result, "Should handle state comparison case-insensitively");
        }

        #endregion

        #region Helper Methods

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

        #region TestDevOpsConnection

        private class TestDevOpsConnection : IDevOpsConnection
        {
            private readonly TestWorkItemClient _workItemClient = new();

            public BuildHttpClient GetBuildClient()
            {
                throw new NotImplementedException();
            }

            public WorkItemTrackingHttpClient GetWorkItemClient()
            {
                return _workItemClient;
            }

            public ProjectHttpClient GetProjectClient()
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
                var result = new WorkItemQueryResult
                {
                    WorkItems = _queryWorkItems.Select(wi => new WorkItemReference { Id = wi.Id ?? 0 }).ToList()
                };
                return Task.FromResult(result);
            }

            public override Task<List<WorkItem>> GetWorkItemsAsync(
                IEnumerable<int> ids,
                IEnumerable<string>? fields = null,
                DateTime? asOf = null,
                WorkItemExpand? expand = null,
                WorkItemErrorPolicy? errorPolicy = null,
                string? project = null,
                object? userState = null,
                CancellationToken cancellationToken = default)
            {
                var workItems = _queryWorkItems.Where(wi => ids.Contains(wi.Id ?? 0)).ToList();
                return Task.FromResult(workItems);
            }

            public override Task<WorkItem> GetWorkItemAsync(
                int id,
                IEnumerable<string>? fields = null,
                DateTime? asOf = null,
                WorkItemExpand? expand = null,
                string? project = null,
                object? userState = null,
                CancellationToken cancellationToken = default)
            {
                if (_workItems.TryGetValue(id, out var workItem))
                {
                    return Task.FromResult(workItem);
                }
                throw new Exception($"Work item {id} not found");
            }
        }

        #endregion
    }
}
