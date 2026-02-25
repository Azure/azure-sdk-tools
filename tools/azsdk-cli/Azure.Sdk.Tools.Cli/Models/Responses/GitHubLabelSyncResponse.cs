// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses;

public class GitHubLabelSyncResponse : CommandResponse
{
    [JsonPropertyName("existing_work_items")]
    public List<GitHubLableWorkItem> ExistingWorkItems { get; set; } = [];

    [JsonPropertyName("created_work_items")]
    public List<GitHubLableWorkItem> CreatedWorkItems { get; set; } = [];

    [JsonPropertyName("sync_errors")]
    public List<GitHubLabelSyncError> SyncErrors { get; set; } = [];

    [JsonPropertyName("dry_run")]
    public bool DryRun { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Dry Run: {DryRun}");
        sb.AppendLine();

        if (ExistingWorkItems.Count > 0)
        {
            sb.AppendLine($"Existing Work Items ({ExistingWorkItems.Count}):");
            foreach (var item in ExistingWorkItems)
            {
                sb.AppendLine($"  - {item}");
            }
            sb.AppendLine();
        }

        if (CreatedWorkItems.Count > 0)
        {
            sb.AppendLine($"{(DryRun ? "Work Items to Create" : "Created Work Items")} ({CreatedWorkItems.Count}):");
            foreach (var item in CreatedWorkItems)
            {
                sb.AppendLine($"  - {item}");
            }
            sb.AppendLine();
        }

        if (SyncErrors.Count > 0)
        {
            sb.AppendLine($"Errors ({SyncErrors.Count}):");
            foreach (var error in SyncErrors)
            {
                sb.AppendLine($"  - {error}");
            }
        }

        return sb.ToString();
    }
}
