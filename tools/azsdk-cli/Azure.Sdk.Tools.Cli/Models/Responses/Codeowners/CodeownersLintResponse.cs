// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

/// <summary>
/// Structured result for the CODEOWNERS lint command.
/// </summary>
public class CodeownersLintResponse : CommandResponse
{
    [JsonPropertyName("repo_root")]
    public string RepoRoot { get; set; } = string.Empty;

    [JsonPropertyName("violations")]
    public List<LintViolation> Violations { get; } = [];

    [JsonPropertyName("total_violations")]
    public int TotalViolations => Violations.Count;

    protected override string Format()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== CODEOWNERS Lint Report ===");
        sb.AppendLine($"Repo root: {RepoRoot}");
        sb.AppendLine($"Total violations: {TotalViolations}");

        if (Violations.Count == 0)
        {
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine();

        foreach (var ruleGroup in Violations.GroupBy(v => v.RuleId).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"--- {ruleGroup.Key} ({ruleGroup.Count()} violations) ---");
            foreach (var violation in ruleGroup)
            {
                sb.AppendLine($"  {violation.Description}");
                if (!string.IsNullOrEmpty(violation.SourceFile))
                {
                    sb.AppendLine($"    At: {violation.SourceFile}");
                }

                if (!string.IsNullOrEmpty(violation.Detail))
                {
                    sb.AppendLine($"    Detail: {violation.Detail}");
                }
            }

            sb.AppendLine();
        }

        sb.AppendLine("See https://aka.ms/azsdk/codeowners for how to fix these.");

        return sb.ToString().TrimEnd();
    }
}
