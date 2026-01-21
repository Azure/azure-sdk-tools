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
                sb.AppendLine($"        Instructions: {string.Join(", ", result.Instructions)}");
                if (!string.IsNullOrEmpty(result.Reason))
                {
                    sb.AppendLine($"        Reason: {result.Reason}\n");
                }
                sb.AppendLine($"        Requirement Status Details: {result.RequirementStatusDetails}\n");
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
}
