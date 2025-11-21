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

        if (Results != null)
        {
            foreach (var result in Results)
            {
                sb.AppendLine($"  - Requirement: {result.Requirement}");
                sb.AppendLine($"    Instructions: {string.Join(", ", result.Instructions)}");
                sb.AppendLine($"    Output: {result.Output}");
            }
        }
        else
        {
            sb.AppendLine("  None");
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
    /// Output from running the requirement check.
    /// </summary>
    public string? Output { get; set; }
}
