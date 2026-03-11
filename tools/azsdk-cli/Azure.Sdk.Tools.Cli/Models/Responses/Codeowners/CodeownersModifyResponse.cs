// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

/// <summary>
/// Response returned by the CODEOWNERS add and remove commands.
/// Includes a description of the operation performed and a view of the resulting state.
/// </summary>
public class CodeownersModifyResponse : CommandResponse
{
    [JsonPropertyName("view")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CodeownersViewResponse? View { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();
        if (View != null)
        {
            var viewText = View.ToString();
            if (!string.IsNullOrWhiteSpace(viewText))
            {
                sb.Append(viewText);
            }
        }
        return sb.ToString();
    }
}
