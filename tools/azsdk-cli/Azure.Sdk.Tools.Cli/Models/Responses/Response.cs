// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class Response
{
    [JsonPropertyName("response_error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResponseError { get; set; }

    [JsonPropertyName("response_errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string> ResponseErrors { get; set; }

    [JsonPropertyName("next_steps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? NextSteps { get; set; }

    protected string ToString(StringBuilder value)
    {
        return ToString(value.ToString());
    }

    protected string ToString(string value)
    {
        List<string> messages = [];
        if (!string.IsNullOrEmpty(ResponseError))
        {
            messages.Add("[ERROR] " + ResponseError);
        }
        foreach (var error in ResponseErrors ?? [])
        {
            messages.Add("[ERROR] " + error);
        }

        if (NextSteps?.Count > 0)
{
            messages.Add("[NEXT STEPS]");
            foreach (var step in NextSteps)
            {
                messages.Add(step);
            }
        }

        if (messages.Count > 0)
        {
            value = string.Join(Environment.NewLine, messages);
        }

        return value;
    }
}
