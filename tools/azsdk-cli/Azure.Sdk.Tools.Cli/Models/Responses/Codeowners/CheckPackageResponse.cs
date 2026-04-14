// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

/// <summary>
/// Response for a successful check-package command. Failures are reported separately as error responses by the command.
/// </summary>
public class CheckPackageResponse : CommandResponse
{
    [JsonPropertyName("directory_path")]
    public string DirectoryPath { get; set; } = string.Empty;

    [JsonPropertyName("owners")]
    public List<string> Owners { get; set; } = new();

    [JsonPropertyName("pr_labels")]
    public List<string> PRLabels { get; set; } = new();

    [JsonPropertyName("service_owners")]
    public List<string> ServiceOwners { get; set; } = new();

    [JsonPropertyName("service_labels")]
    public List<string> ServiceLabels { get; set; } = new();

    protected override string Format()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Path: {DirectoryPath}");
        sb.AppendLine($"Owners: {string.Join(", ", Owners)}");
        sb.AppendLine($"PR Labels: {string.Join(", ", PRLabels)}");
        sb.AppendLine($"Service Labels: {string.Join(", ", ServiceLabels)}");
        sb.AppendLine($"Service Owners: {string.Join(", ", ServiceOwners)}");
        return sb.ToString();
    }
}
