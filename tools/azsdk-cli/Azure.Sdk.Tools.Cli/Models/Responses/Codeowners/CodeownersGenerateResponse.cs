// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

/// <summary>
/// Structured result for <c>generate</c>. Dropped entries are reported, not treated as failures:
/// a repository always contains some decayed ownership, and refusing to render would leave the
/// checked-in CODEOWNERS frozen at whatever it happened to say.
/// </summary>
public class CodeownersGenerateResponse : CommandResponse
{
    [JsonPropertyName("output_path")]
    public string OutputPath { get; set; } = string.Empty;

    [JsonPropertyName("dropped")]
    public IReadOnlyList<DroppedItem> Dropped { get; set; } = [];

    protected override string Format()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Wrote {OutputPath}.");

        if (Dropped.Count == 0)
        {
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine();
        sb.AppendLine($"Excluded {Dropped.Count} item(s) from the rendered file:");

        foreach (var item in Dropped)
        {
            var where = string.IsNullOrEmpty(item.Where) ? string.Empty : $"{item.Where}: ";
            var subject = string.IsNullOrEmpty(item.Subject) ? string.Empty : $"'{item.Subject}' — ";
            sb.AppendLine($"  [{item.RuleId}] {where}{subject}{item.Reason}");
        }

        return sb.ToString().TrimEnd();
    }
}
