// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public sealed class CustomizedCodeRepairInput
{
    [JsonPropertyName("repositoryRoot")]
    public required string RepositoryRoot { get; set; }

    [JsonPropertyName("packagePath")]
    public required string PackagePath { get; set; }

    [JsonPropertyName("editScope")]
    public string EditScope => "CustomCode";

    [JsonPropertyName("requestSha256")]
    public required string RequestSha256 { get; set; }

    [JsonPropertyName("baseHead")]
    public required string BaseHead { get; set; }

    [JsonPropertyName("initialTree")]
    public required string InitialTree { get; set; }

    [JsonPropertyName("tspLocationPath")]
    public required string TspLocationPath { get; set; }

    [JsonPropertyName("tspLocationSha256")]
    public string? TspLocationSha256 { get; set; }

    [JsonPropertyName("localSpec")]
    public CustomizedCodeRepairLocalSpec? LocalSpec { get; set; }
}
