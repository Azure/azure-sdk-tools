// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses;

/// <summary>
/// Response model for the GetTypeSpecTopicsDocsTool
/// </summary>
public class GetTypeSpecTopicsDocsResponse : Response
{
    [JsonPropertyName("docs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<TypeSpecTopicDoc> Docs { get; set; } = [];

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
            return ToString($"Retrieved documentation for {Docs.Count} topics");
        }
    }
}
