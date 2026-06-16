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
        public const string TypeSpecCustomizationFailed = "TypeSpecCustomizationFailed";
        public const string ManualInterventionRequired = "ManualInterventionRequired";

        /// <summary>
        /// Returned when spec inputs are out of scope (<see cref="Models.EditScope.SpecInputs"/> not set):
        /// the failure can only be fixed by editing the spec inputs (client.tsp / tspconfig.yaml) or moving
        /// the pinned spec commit, which belongs in a separate spec-repo PR.
        /// </summary>
        public const string SpecChangeRequired = "SpecChangeRequired";
    }

    /// <summary>
    /// Populated when spec inputs are out of scope: items that cannot be fixed by editing custom code and
    /// instead require a spec-repo change (e.g. a <c>@@clientName</c>/<c>@@access</c> decorator in
    /// <c>client.tsp</c>). These are reported, not applied — spec inputs are never edited in this scope.
    /// </summary>
    [JsonPropertyName("specChangeRequired")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? SpecChangeRequired { get; set; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("errorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

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
        if (!string.IsNullOrWhiteSpace(BuildResult))
        {
            sb.AppendLine("Build Output:");
            sb.AppendLine(BuildResult);
        }
        if (TypeSpecChangesSummary is { Count: > 0 })
        {
            sb.AppendLine("TypeSpec Changes:");
            foreach (var change in TypeSpecChangesSummary)
            {
                sb.AppendLine($"  - {change}");
            }
        }
        if (AppliedPatches is { Count: > 0 })
        {
            sb.AppendLine("Code customization patches:");
            foreach (var patch in AppliedPatches)
            {
                sb.AppendLine($"  - {patch.FilePath}: {patch.Description} ({patch.ReplacementCount} replacement(s))");
            }
        }
        if (SpecChangeRequired is { Count: > 0 })
        {
            sb.AppendLine("Requires a separate spec-repo PR (out of scope for custom-code repair):");
            foreach (var item in SpecChangeRequired)
            {
                sb.AppendLine($"  - {item}");
            }
        }
        return sb.ToString();
    }
}
