// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public sealed class CustomizedCodeRepairLocalSpec
{
    [JsonPropertyName("projectPath")]
    public required string ProjectPath { get; set; }

    [JsonPropertyName("repositoryRoot")]
    public required string RepositoryRoot { get; set; }

    [JsonPropertyName("initialHead")]
    public required string InitialHead { get; set; }

    [JsonPropertyName("initialTree")]
    public required string InitialTree { get; set; }

    [JsonPropertyName("finalHead")]
    public string? FinalHead { get; set; }

    [JsonPropertyName("finalTree")]
    public string? FinalTree { get; set; }
}
