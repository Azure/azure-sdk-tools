// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Response model for Azure service example operations
/// </summary>
public class ExampleServiceResponse : Response
{
    [JsonPropertyName("service_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceName { get; set; }

    [JsonPropertyName("operation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Operation { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Result { get; set; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Details { get; set; }

    public override string ToString()
    {
        var output = new List<string>();

        if (!string.IsNullOrEmpty(ServiceName))
            output.Add($"Service: {ServiceName}");

        if (!string.IsNullOrEmpty(Operation))
            output.Add($"Operation: {Operation}");

        if (!string.IsNullOrEmpty(Result))
            output.Add($"Result: {Result}");

        if (Details?.Any() == true)
        {
            output.Add("Details:");
            foreach (var detail in Details)
            {
                output.Add($"  {detail.Key}: {detail.Value}");
            }
        }

        var formatted = string.Join(Environment.NewLine, output);
        return ToString(formatted); // Calls base method to include error formatting
    }
}
