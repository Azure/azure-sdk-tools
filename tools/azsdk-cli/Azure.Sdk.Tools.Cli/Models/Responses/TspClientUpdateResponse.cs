// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Response payload for TspClientUpdateTool MCP / CLI operations.
/// </summary>
public class TspClientUpdateResponse : CommandResponse
{
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("errorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(Message))
        {
            sb.AppendLine(Message);
        }
        if (!string.IsNullOrWhiteSpace(ErrorCode))
        {
            sb.AppendLine($"ErrorCode: {ErrorCode}");
        }
        return sb.ToString();
    }
}
