// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class TelemetryIngestionResponse : CommandResponse
{
    [JsonPropertyName("event_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EventType { get; set; }

    [JsonPropertyName("client_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientType { get; set; }

    [JsonPropertyName("session_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SessionId { get; set; }

    [JsonPropertyName("skill_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SkillName { get; set; }

    protected override string Format()
    {
        if (string.IsNullOrEmpty(EventType))
        {
            return string.Empty;
        }

        var client = string.IsNullOrEmpty(ClientType) ? "unknown client" : ClientType;
        return $"Recorded telemetry event '{EventType}' from {client}.";
    }
}
