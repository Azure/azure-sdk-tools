// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;

/// <summary>
/// Original native detector evidence and baseline provenance, preserved independently of AI
/// classification. These details supplement, rather than replace, changes and hasBreakingChange.
/// </summary>
public class SdkChangeDetails
{
    /// <summary>The released baseline used by the native detector; null when none was available.</summary>
    [JsonPropertyName("baselineVersion")]
    public string? BaselineVersion { get; set; }

    /// <summary>Structured .NET API differences, including supplementary additions.</summary>
    [JsonPropertyName("apiChanges")]
    public List<DotnetSdkApiChange> ApiChanges { get; set; } = [];

    [JsonPropertyName("diagnostics")]
    public List<string> Diagnostics { get; set; } = [];

    [JsonPropertyName("limitations")]
    public List<string> Limitations { get; set; } = [];

    /// <summary>Additional detector provenance and evidence retained without interpretation.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
