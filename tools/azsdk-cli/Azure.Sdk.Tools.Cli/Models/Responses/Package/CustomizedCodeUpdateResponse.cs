// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package;

/// <summary>
/// Represents a single patch that was applied to a customization file.
/// </summary>
public record AppliedPatch(
    string FilePath,
    string Description,
    int ReplacementCount);

/// <summary>
/// Response payload for CustomizedCodeUpdateTool MCP / CLI operations returns success/failure with build result.
/// </summary>
public class CustomizedCodeUpdateResponse : PackageResponseBase
{
    /// <summary>
    /// Indicates whether the update operation succeeded (build passed after patches).
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("appliedPatches")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AppliedPatch>? AppliedPatches { get; set; }

    /// <summary>
    /// Raw build error output. Only set when Success = false.
    /// The classifier uses this to determine next steps.
    /// </summary>
    [JsonPropertyName("buildResult")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BuildResult { get; set; }


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
    }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("errorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

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
        return sb.ToString();
    }
}
