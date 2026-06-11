// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

/// <summary>
/// Structured result for the CODEOWNERS audit command.
/// </summary>
public class CodeownersAuditResponse : CommandResponse
{
    [JsonPropertyName("fix_requested")]
    public bool FixRequested { get; set; }

    [JsonPropertyName("force_requested")]
    public bool ForceRequested { get; set; }

    [JsonPropertyName("repo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; set; }

    [JsonPropertyName("violations")]
    public List<AuditViolation> Violations { get; } = [];

    [JsonPropertyName("fix_results")]
    public List<AuditFixResult> FixResults { get; } = [];

    [JsonPropertyName("total_violations")]
    public int TotalViolations => Violations.Count;

    [JsonPropertyName("fixes_applied")]
    public int FixesApplied => FixResults.Count(r => r.Success);

    [JsonPropertyName("fixes_failed")]
    public int FixesFailed => FixResults.Count(r => !r.Success);

    protected override string Format()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== CODEOWNERS Audit Report ===");
        sb.AppendLine($"Fix mode: {FixRequested}, Force: {ForceRequested}, Repo: {Repo ?? "(all)"}");
        sb.AppendLine($"Total violations: {TotalViolations}");
        sb.AppendLine($"Fixes applied: {FixesApplied}");
        sb.AppendLine($"Fixes failed: {FixesFailed}");

        if (Violations.Count > 0)
        {
            sb.AppendLine();
            foreach (var ruleGroup in Violations.GroupBy(v => v.RuleId).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"--- {ruleGroup.Key} ({ruleGroup.Count()} violations) ---");
                foreach (var violation in ruleGroup)
                {
                    sb.AppendLine($"  {violation.Description}");
                    if (!string.IsNullOrEmpty(violation.Detail))
                    {
                        sb.AppendLine($"    Detail: {violation.Detail}");
                    }
                }
                sb.AppendLine();
            }
        }

        if (FixResults.Count > 0)
        {
            sb.AppendLine("--- Fix Results ---");
            foreach (var fixResult in FixResults)
            {
                var status = fixResult.Success ? "SUCCESS" : "FAILED";
                sb.AppendLine($"  [{status}] {fixResult.Description}");
                if (!string.IsNullOrEmpty(fixResult.ErrorMessage))
                {
                    sb.AppendLine($"    Error: {fixResult.ErrorMessage}");
                }
            }
        }

        return sb.ToString().TrimEnd();
    }
}
