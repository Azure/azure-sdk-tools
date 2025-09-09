// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Represents a TypeSpec documentation topic with its content
/// </summary>
public class TypeSpecTopicDoc
{
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("contents")]
    public string Contents { get; set; } = string.Empty;
}
