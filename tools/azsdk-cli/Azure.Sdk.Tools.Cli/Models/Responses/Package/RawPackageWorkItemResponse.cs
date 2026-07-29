// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public class RawPackageWorkItemResponse : CommandResponse
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("rev")]
    public int? Rev { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public IDictionary<string, object>? Fields { get; set; }

    [JsonPropertyName("relations")]
    public IList<WorkItemRelation>? Relations { get; set; }

    protected override string Format()
    {
        var output = new StringBuilder();
        if (Id is not null)
        {
            output.AppendLine($"Work Item ID: {Id}");
        }
        if (Rev is not null)
        {
            output.AppendLine($"Revision: {Rev}");
        }
        if (!string.IsNullOrWhiteSpace(Url))
        {
            output.AppendLine($"URL: {Url}");
        }
        if (Fields?.Count > 0)
        {
            output.AppendLine($"Fields: {Fields.Count}");
        }
        if (Relations?.Count > 0)
        {
            output.AppendLine($"Relations: {Relations.Count}");
        }

        return output.ToString().TrimEnd();
    }
}
