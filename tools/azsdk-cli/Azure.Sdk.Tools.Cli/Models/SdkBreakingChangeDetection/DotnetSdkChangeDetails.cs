// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;

/// <summary>Native ApiCompat evidence and baseline provenance for a .NET SDK package.</summary>
public class DotnetSdkChangeDetails : SdkChangeDetails
{
    /// <summary>The released baseline used by the native detector; null when none was available.</summary>
    [JsonPropertyName("baselineVersion")]
    public string? BaselineVersion { get; set; }

    [JsonPropertyName("apiChanges")]
    public List<DotnetSdkApiChange> ApiChanges { get; set; } = [];

    [JsonPropertyName("diagnostics")]
    public List<string> Diagnostics { get; set; } = [];

    [JsonPropertyName("limitations")]
    public List<string> Limitations { get; set; } = [];
}
