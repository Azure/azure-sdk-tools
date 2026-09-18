// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public sealed class CustomizedCodeRepairState
{
    [JsonPropertyName("head")]
    public required string Head { get; set; }

    [JsonPropertyName("tree")]
    public required string Tree { get; set; }

    [JsonPropertyName("tspLocationSha256")]
    public string? TspLocationSha256 { get; set; }

    [JsonPropertyName("changedFiles")]
    public List<CustomizedCodeRepairChange> ChangedFiles { get; set; } = [];
}
