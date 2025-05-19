// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class GenericResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public List<string> Details { get; set; } = [];

    public override string ToString()
    {
        return $"Status: {Status}" +
               $"\nDetails:\n" +
               $"{string.Join("\n- ", Details)}\n";
    }
}