// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Models;

public class PrChecksResponse : CommandResponse
{
    [JsonPropertyName("checks")]
    public List<PrCheckRun> Checks { get; set; } = [];

    [JsonPropertyName("pr_link")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrLink { get; set; }

    protected override string Format()
    {
        if (Checks.Count == 0)
        {
            return "No checks found.";
        }

        var output = $"Found {Checks.Count} check(s):" + Environment.NewLine;
        foreach (var check in Checks)
        {
            var status = check.Conclusion ?? "PENDING";
            output += $"  [{status}] {check.Name} ({check.AppName})" + Environment.NewLine;
            if (!string.IsNullOrEmpty(check.DetailsUrl))
            {
                output += $"    {check.DetailsUrl}" + Environment.NewLine;
            }
        }
        return output;
    }
}
