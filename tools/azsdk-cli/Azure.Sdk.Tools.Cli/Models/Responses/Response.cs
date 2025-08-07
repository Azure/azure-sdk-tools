// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
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

    protected string ToString(string value)
    {
        List<string> errors = [];
        if (!string.IsNullOrEmpty(ResponseError))
        {
            errors.Add("[ERROR] " + ResponseError);
        }
        foreach (var error in ResponseErrors ?? [])
        {
            errors.Add("[ERROR] " + error);
        }

        if (errors.Count > 0)
        {
            value = string.Join(Environment.NewLine, errors);
        }

        return value;
    }
}
