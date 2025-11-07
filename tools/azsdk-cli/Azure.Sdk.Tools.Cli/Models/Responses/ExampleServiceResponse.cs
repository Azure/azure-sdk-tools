// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Response model for Azure service example operations
/// </summary>
public class ExampleServiceResponse : CommandResponse
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

    protected override string Format()
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(ServiceName))
        {
            sb.AppendLine($"Service: {ServiceName}");
        }

        if (!string.IsNullOrEmpty(Operation))
        {
            sb.AppendLine($"Operation: {Operation}");
        }

        if (!string.IsNullOrEmpty(Result))
        {
            sb.AppendLine($"Result: {Result}");
        }

        if (Details?.Any() == true)
        {
            sb.AppendLine("Details:");
            foreach (var detail in Details)
            {
                sb.AppendLine($"  {detail.Key}: {detail.Value}");
            }
        }

        return sb.ToString();
    }
}
