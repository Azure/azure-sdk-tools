// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

/// <summary>
/// Response payload for CustomizedCodeUpdateTool MCP / CLI operations.
/// </summary>
public class CustomizedCodeUpdateResponse : PackageResponseBase
{
    /// <summary>
    /// Error codes for classifier to parse programmatically.
    /// These define the contract between the tool and downstream processors.
    /// </summary>
    public static class KnownErrorCodes
    {
        public const string RegenerateFailed = "RegenerateFailed";
        public const string RegenerateAfterPatchesFailed = "RegenerateAfterPatchesFailed";
        public const string BuildAfterPatchesFailed = "BuildAfterPatchesFailed";
        public const string BuildNoCustomizationsFailed = "BuildNoCustomizationsFailed";
        public const string PatchesFailed = "PatchesFailed";
        public const string NoLanguageService = "NoLanguageService";
        public const string InvalidInput = "InvalidInput";
        public const string UnexpectedError = "UnexpectedError";
        public const string TypeSpecCustomizationFailed = "TypeSpecCustomizationFailed";
    }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("errorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Summary of TypeSpec client.tsp changes applied during the TypeSpec Customizations phase.
    /// </summary>
    [JsonPropertyName("typeSpecChangesSummary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? TypeSpecChangesSummary { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(Message))
        {
            sb.AppendLine(Message);
        }
        if (!string.IsNullOrWhiteSpace(ErrorCode))
        {
            sb.AppendLine($"ErrorCode: {ErrorCode}");
        }
        if (TypeSpecChangesSummary is { Count: > 0 })
        {
            sb.AppendLine("TypeSpec Changes:");
            foreach (var change in TypeSpecChangesSummary)
            {
                sb.AppendLine($"  - {change}");
            }
        }
        return sb.ToString();
    }
}
