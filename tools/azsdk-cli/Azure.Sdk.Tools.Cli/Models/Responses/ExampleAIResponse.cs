// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Response model for AI service example operations
/// </summary>
public class ExampleAIResponse : Response
{
    [JsonPropertyName("prompt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prompt { get; set; }

    [JsonPropertyName("response_text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResponseText { get; set; }

    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; set; }

    [JsonPropertyName("token_usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? TokenUsage { get; set; }

    public override string ToString()
    {
        var output = new List<string>();

        if (!string.IsNullOrEmpty(Prompt))
            output.Add($"Prompt: {Prompt}");

        if (!string.IsNullOrEmpty(Model))
            output.Add($"Model: {Model}");

        if (!string.IsNullOrEmpty(ResponseText))
            output.Add($"AI Response: {ResponseText}");

        if (TokenUsage?.Any() == true)
        {
            output.Add("Token Usage:");
            foreach (var usage in TokenUsage)
            {
                output.Add($"  {usage.Key}: {usage.Value}");
            }
        }

        var formatted = string.Join(Environment.NewLine, output);
        return ToString(formatted); // Calls base method to include error formatting
    }
}
