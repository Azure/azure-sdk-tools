// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public class PackageWorkItemLookupResponse : CommandResponse
{
    [JsonPropertyName("work_item_id")]
    public int WorkItemId { get; set; }

    [JsonPropertyName("package_name")]
    public string PackageName { get; set; } = string.Empty;

    [JsonPropertyName("package_version_major_minor")]
    public string PackageVersionMajorMinor { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    protected override string Format()
    {
        var output = new StringBuilder();
        output.AppendLine($"Work Item ID: {WorkItemId}");
        return output.ToString();
    }
}