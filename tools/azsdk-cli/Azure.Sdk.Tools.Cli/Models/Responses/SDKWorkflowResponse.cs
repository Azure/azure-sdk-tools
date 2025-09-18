// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class SDKWorkflowResponse : CommandResponse
{
    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Status { get; set; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string> Details { get; set; } = [];

    public override string ToString()
    {
        var result = new StringBuilder();
        result.AppendLine($"Status: {Status}");
        foreach (var detail in Details)
        {
            result.AppendLine($"- {detail}");
        }
        return ToString(result);
    }
}
