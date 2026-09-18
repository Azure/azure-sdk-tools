// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

public sealed class CustomizedCodeRepairValidation
{
    [JsonPropertyName("succeeded")]
    public bool Succeeded { get; set; }

    [JsonPropertyName("validatedTree")]
    public string? ValidatedTree { get; set; }

    [JsonPropertyName("buildStageId")]
    public string? BuildStageId { get; set; }

    [JsonPropertyName("requiredStageIds")]
    public List<string> RequiredStageIds { get; set; } = [];
}
