// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.SdkBreakingChangeDetection;

/// <summary>
/// Language-neutral detector metadata, preserved independently of AI classification.
/// Unrecognized language schemas round-trip without requiring a type discriminator.
/// </summary>
[JsonDerivedType(typeof(DotnetSdkChangeDetails))]
public class SdkChangeDetails
{
    /// <summary>Additional detector provenance and evidence retained without interpretation.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
