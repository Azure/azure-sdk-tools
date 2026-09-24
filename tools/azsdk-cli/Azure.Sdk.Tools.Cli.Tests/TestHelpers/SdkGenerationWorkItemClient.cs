// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using DevOpsPatch = Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument;
using DevOpsPatchOperation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation;

namespace Azure.Sdk.Tools.Cli.Tests.TestHelpers
{
    // Offline work item snapshots with atomic JSON Patch tests and controllable concurrent writers.
    internal sealed class SdkGenerationWorkItemClient : WorkItemTrackingHttpClient
    {
        private readonly Dictionary<int, WorkItem> _workItems = [];
        private readonly Dictionary<int, int> _readCounts = [];

        public List<(int Id, WorkItemExpand? Expand, CancellationToken Token)> Reads { get; } = [];
        public List<(int Id, DevOpsPatch Patch, CancellationToken Token)> UpdateAttempts { get; } = [];
        public int SuccessfulUpdates { get; private set; }
        public Action<int, int, WorkItem>? BeforeRead { get; set; }
        public Action<WorkItem>? BeforeUpdate { get; set; }
        public Exception? ReadFailure { get; set; }
        public int? ReadFailureId { get; set; }
        public Exception? UpdateFailure { get; set; }

        public SdkGenerationWorkItemClient() : base(new Uri("https://dev.azure.com/test"), new VssCredentials())
        {
        }

        public void AddWorkItem(WorkItem workItem)
        {
            _workItems.Add(workItem.Id!.Value, Snapshot(workItem));
        }

        public void ChangeWorkItem(int id, Action<WorkItem> change)
        {
            change(_workItems[id]);
        }

        public WorkItem GetStoredWorkItem(int id)
        {
            return Snapshot(_workItems[id]);
        }

        public override Task<WorkItem> GetWorkItemAsync(
            int id,
            IEnumerable<string>? fields = null,
            DateTime? asOf = null,
            WorkItemExpand? expand = null,
            object? userState = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Reads.Add((id, expand, cancellationToken));
            if (ReadFailure != null && (ReadFailureId == null || ReadFailureId == id))
            {
                throw ReadFailure;
            }
            var workItem = _workItems[id];
            var count = _readCounts.GetValueOrDefault(id) + 1;
            _readCounts[id] = count;
            BeforeRead?.Invoke(id, count, workItem);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Snapshot(workItem));
        }

        public override Task<WorkItem> UpdateWorkItemAsync(
            DevOpsPatch document,
            int id,
            bool? validateOnly = null,
            bool? bypassRules = null,
            bool? suppressNotifications = null,
            WorkItemExpand? expand = null,
            object? userState = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpdateAttempts.Add((id, document, cancellationToken));
            var workItem = _workItems[id];
            BeforeUpdate?.Invoke(workItem);
            cancellationToken.ThrowIfCancellationRequested();
            if (UpdateFailure != null)
            {
                throw UpdateFailure;
            }

            // All preconditions must pass before any field is changed, just like an ADO conditional patch.
            foreach (var operation in document.Where(operation => operation.Operation == DevOpsPatchOperation.Test))
            {
                object? actual;
                if (operation.Path == "/rev")
                {
                    actual = workItem.Rev;
                }
                else if (operation.Path.StartsWith("/fields/", StringComparison.Ordinal))
                {
                    workItem.Fields.TryGetValue(operation.Path["/fields/".Length..], out actual);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported conditional path '{operation.Path}'.");
                }
                if (!Equals(actual, operation.Value))
                {
                    throw new InvalidOperationException($"Conditional update failed for {operation.Path}.");
                }
            }

            var updated = Snapshot(workItem);
            foreach (var operation in document.Where(operation => operation.Operation != DevOpsPatchOperation.Test))
            {
                if (operation.Operation != DevOpsPatchOperation.Add || !operation.Path.StartsWith("/fields/", StringComparison.Ordinal))
                {
                    throw new NotSupportedException($"Unsupported completion operation '{operation.Operation}' at '{operation.Path}'.");
                }
                updated.Fields[operation.Path["/fields/".Length..]] = operation.Value;
            }
            updated.Rev = (updated.Rev ?? 0) + 1;
            _workItems[id] = updated;
            SuccessfulUpdates++;
            return Task.FromResult(Snapshot(updated));
        }

        private static WorkItem Snapshot(WorkItem workItem)
        {
            return new WorkItem
            {
                Id = workItem.Id,
                Rev = workItem.Rev,
                Url = workItem.Url,
                Fields = new Dictionary<string, object>(workItem.Fields),
                Relations = workItem.Relations?.Select(relation => new WorkItemRelation
                {
                    Rel = relation.Rel,
                    Url = relation.Url,
                    Attributes = relation.Attributes == null ? null : new Dictionary<string, object>(relation.Attributes)
                }).ToList()
            };
        }
    }
}