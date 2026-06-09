// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Configuration;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public class PackageWorkItemLookupResponse : CommandResponse
{
    private string workItemUrl = string.Empty;

    [JsonPropertyName("work_item_id")]
    public int WorkItemId { get; set; }

    [JsonPropertyName("work_item_url")]
    public string WorkItemUrl
    {
        get => WorkItemId > 0
            ? $"{Constants.AZURE_SDK_DEVOPS_BASE_URL}/{Constants.AZURE_SDK_DEVOPS_RELEASE_PROJECT}/_workitems/edit/{WorkItemId}"
            : workItemUrl.Replace("_apis/wit/workItems", "_workitems/edit");
        set => workItemUrl = value;
    }

    [JsonPropertyName("package_name")]
    public string PackageName { get; set; } = string.Empty;

    [JsonPropertyName("package_version_major_minor")]
    public string PackageVersionMajorMinor { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    protected override string Format()
    {
        var output = new StringBuilder();
        output.AppendLine(WorkItemUrl);
        output.AppendLine($"Work Item ID: {WorkItemId}");
        output.AppendLine($"Package: {PackageName}");
        output.AppendLine($"Version: {PackageVersionMajorMinor}");
        output.AppendLine($"Language: {Language}");
        return output.ToString();
    }
}