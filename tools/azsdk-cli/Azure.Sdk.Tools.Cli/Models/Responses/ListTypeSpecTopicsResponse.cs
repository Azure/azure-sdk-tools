// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses;

/// <summary>
/// Response model for the ListTypeSpecTopicsTool
/// </summary>
public class ListTypeSpecTopicsResponse : Response
{
    [JsonPropertyName("topics")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<TypeSpecTopic> Topics { get; set; } = new List<TypeSpecTopic>();

    [JsonPropertyName("is_successful")]
    public bool IsSuccessful { get; set; }

    public override string ToString()
    {
        if (!IsSuccessful)
        {
            return ToString(string.Empty);
        }
        else
        {
            return ToString($"Found {Topics.Count} TypeSpec documentation topics");
        }
    }
}
