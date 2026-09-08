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
    /// Violations exit non-zero so lint can gate a pull request build, but they are not a
    /// <c>ResponseError</c>: the command ran fine and produced a report. Setting ResponseError here
    /// would mark the run Failed, and a Failed response prints only the error line — which would
    /// throw away the very report the contributor needs to fix their file.
    /// </summary>
    public override int ExitCode => TotalViolations == 0 ? base.ExitCode : 1;

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
