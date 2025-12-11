// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Attributes;
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

public class ReleaseWorkflowResponse : ReleasePlanBaseResponse
{
    [Telemetry]
    [JsonPropertyName("language")]
    public SdkLanguage Language { get; set; }
    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Status { get; set; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string> Details { get; set; } = [];

    protected override string Format()
    {
        var result = new StringBuilder();
        result.AppendLine($"Status: {Status}");
        foreach (var detail in Details)
        {
            result.AppendLine($"- {detail}");
        }
        return result.ToString();
    }
    public void SetLanguage(string language)
    {
        Language = SdkLanguageHelpers.GetSdkLanguage(language);
    }
}
