// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class GitHubLableWorkItem
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("work_item_id")]
    public int WorkItemId { get; set; }

    [JsonPropertyName("work_item_url")]
    public string WorkItemUrl { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{Label} (ID: {WorkItemId}, URL: {WorkItemUrl})";
    }
}
