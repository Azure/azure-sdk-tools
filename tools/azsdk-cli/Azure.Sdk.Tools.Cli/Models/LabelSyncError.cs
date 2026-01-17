// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LabelSyncErrorType
{
    DuplicateCsvLabel,
    DuplicateAdoWorkItem,
    OrphanedWorkItem,
    AdoApiError
}

public class LabelSyncError
{
    [JsonPropertyName("error_type")]
    public LabelSyncErrorType ErrorType { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string Details { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"[{ErrorType}] {Label}: {Details}";
    }
}
