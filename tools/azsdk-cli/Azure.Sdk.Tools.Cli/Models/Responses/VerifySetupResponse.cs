// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;
using System.Text;
namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Response model for setup verification operations
/// </summary>
public class VerifySetupResponse : CommandResponse
{
    [JsonPropertyName("results")]
    public List<RequirementCheckResult>? Results { get; set; }

    protected override string Format()
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("Results:");

        if (Results != null && Results.Count > 0)
        {
            foreach (var result in Results)
            {
                sb.AppendLine($"  - Requirement: {result.Requirement}");

                if (result.AutoInstallSucceeded)
                {
                    sb.AppendLine($"        - Auto-installed successfully");
                }
                else if (result.AutoInstallAttempted && !result.AutoInstallSucceeded)
                {
                    sb.AppendLine($"        - Auto-install failed: {result.AutoInstallError}");
                    sb.AppendLine($"        - Instructions: {string.Join(", ", result.Instructions)}");
                }
                else
                {
                    sb.AppendLine($"        - Instructions: {string.Join(", ", result.Instructions)}");
                    if (result.IsAutoInstallable)
                    {
                        sb.AppendLine($"        - Tip: Re-run with 'install' sub-command to install this automatically");
                    }
                }

                if (!string.IsNullOrEmpty(result.NotAutoInstallableReason))
                {
                    sb.AppendLine($"        - Not auto-installable: {result.NotAutoInstallableReason}");
                }

                if (!string.IsNullOrEmpty(result.Reason))
                {
                    sb.AppendLine($"        - Reason: {result.Reason}");
                }
                sb.AppendLine($"        - Requirement Status Details: {result.RequirementStatusDetails}\n");
            }

            
        }
        else
        {
            sb.AppendLine("  Verify setup succeeded, no issues found.");
        }
        return sb.ToString();
    }
}

/// <summary>
/// Represents the result of a requirement check during setup verification.
/// </summary>
public class RequirementCheckResult
{
    /// <summary>
    /// The requirement that was checked.
    /// </summary>
    public string Requirement { get; set; }
    /// <summary>
    /// Instructions for resolving issues found during the requirement check.
    /// </summary>
    public List<string> Instructions { get; set; }
    /// <summary>
    /// Output from any issues encountered during the requirement check.
    /// </summary>
    public string RequirementStatusDetails { get; set; }
    /// <summary>
    /// The reason for the requirement.
    /// </summary>
    public string? Reason { get; set; }
    /// <summary>
    /// Whether auto-install was attempted for this requirement.
    /// Check AutoInstallError to determine success (null = success, non-null = failure).
    /// </summary>
    [JsonPropertyName("autoInstallAttempted")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AutoInstallAttempted { get; set; }
    /// <summary>
    /// Whether auto-install was attempted and succeeded.
    /// </summary>
    [JsonPropertyName("autoInstallSucceeded")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AutoInstallSucceeded => AutoInstallAttempted && string.IsNullOrEmpty(AutoInstallError);
    /// <summary>
    /// Error message if auto-install was attempted but failed.
    /// </summary>
    [JsonPropertyName("autoInstallError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutoInstallError { get; set; }
    /// <summary>
    /// Whether this requirement supports auto-installation.
    /// </summary>
    [JsonPropertyName("isAutoInstallable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsAutoInstallable { get; set; }
    /// <summary>
    /// Reason why this requirement cannot be auto-installed, if applicable.
    /// </summary>
    [JsonPropertyName("notAutoInstallableReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NotAutoInstallableReason { get; set; }
}