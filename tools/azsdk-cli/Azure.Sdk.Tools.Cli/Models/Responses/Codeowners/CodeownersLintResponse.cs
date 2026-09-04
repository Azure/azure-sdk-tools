// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Helpers.Codeowners;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

/// <summary>
/// Structured result for <c>lint-fragments</c>: what is wrong with each fragment, and who each
/// directory beneath it ends up owned by.
/// </summary>
public class CodeownersLintResponse : CommandResponse
{
    [JsonPropertyName("fragments")]
    public IReadOnlyList<FragmentLintResult> Fragments { get; set; } = [];

    [JsonPropertyName("total_violations")]
    public int TotalViolations => Fragments.Sum(fragment => fragment.Violations.Count);

    /// <summary>
    /// Violations fail the command, so lint can gate a pull request build without the caller having
    /// to parse the report to find out whether it passed.
    /// </summary>
    public override string? ResponseError
    {
        get => TotalViolations == 0
            ? base.ResponseError
            : $"lint-fragments found {TotalViolations} violation(s) in {Fragments.Count(f => f.Violations.Count > 0)} file(s).";
        set => base.ResponseError = value;
    }

    protected override string Format()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== owners.yaml Lint Report ===");
        sb.AppendLine($"Fragments checked: {Fragments.Count}");
        sb.AppendLine($"Total violations: {TotalViolations}");

        foreach (var fragment in Fragments)
        {
            sb.AppendLine();
            sb.AppendLine($"--- {fragment.FilePath} ---");

            foreach (var violation in fragment.Violations)
            {
                sb.AppendLine($"  [{violation.RuleId}] {violation.Description}");
                if (!string.IsNullOrEmpty(violation.SourceFile))
                {
                    sb.AppendLine($"    At: {violation.SourceFile}");
                }

                if (!string.IsNullOrEmpty(violation.Detail))
                {
                    sb.AppendLine($"    Detail: {violation.Detail}");
                }
            }

            if (fragment.Violations.Count == 0)
            {
                sb.AppendLine("  No violations.");
            }

            foreach (var directory in fragment.Directories)
            {
                sb.AppendLine(directory.MatchedPath == null
                    ? $"  {directory.Directory}: no owners"
                    : $"  {directory.Directory}: {string.Join(", ", directory.Owners)}");
            }
        }

        if (TotalViolations > 0)
        {
            sb.AppendLine();
            sb.AppendLine("See https://aka.ms/azsdk/codeowners for how to fix these.");
        }

        return sb.ToString().TrimEnd();
    }
}
