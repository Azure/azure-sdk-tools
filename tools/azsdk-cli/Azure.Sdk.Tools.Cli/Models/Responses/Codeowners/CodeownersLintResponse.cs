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

    private const string Red = "\u001b[31m";
    private const string Reset = "\u001b[0m";

    /// <summary>
    /// Each fragment reports its ownership first and its violations second, in red. Ownership is
    /// what the file is for; violations are what has to change. The summary lands at the end so the
    /// counts and the link are the last thing on screen after a long report.
    /// <para>
    /// Only reached in plain-text mode. <see cref="Helpers.OutputHelper.Format"/> serializes JSON
    /// and MCP responses from the properties instead, so no consumer of those sees escape codes.
    /// </para>
    /// </summary>
    protected override string Format()
    {
        var sb = new StringBuilder();

        foreach (var fragment in Fragments)
        {
            sb.AppendLine($"--- {fragment.FilePath} ---");

            foreach (var directory in fragment.Directories)
            {
                sb.AppendLine(directory.MatchedPath == null
                    ? $"  {directory.Directory}: no owners"
                    : $"  {directory.Directory}: {string.Join(", ", directory.Owners)}");
            }

            // A blank line before each violation, so the violations stand apart from the ownership
            // report above them and a multi-line violation does not run into the next one. Skipped
            // for the first violation when there is no ownership report to separate it from.
            var needsSeparator = fragment.Directories.Count > 0;

            foreach (var violation in fragment.Violations)
            {
                if (needsSeparator)
                {
                    sb.AppendLine();
                }

                needsSeparator = true;

                sb.AppendLine(Colorize($"  [{violation.RuleId}] {violation.Description}"));
                if (!string.IsNullOrEmpty(violation.SourceFile))
                {
                    sb.AppendLine(Colorize($"    At: {violation.SourceFile}"));
                }

                if (!string.IsNullOrEmpty(violation.Detail))
                {
                    sb.AppendLine(Colorize($"    Detail: {violation.Detail}"));
                }
            }

            sb.AppendLine();
        }

        sb.AppendLine("=== Lint Report ===");
        sb.AppendLine($"Fragments checked: {Fragments.Count}");
        sb.AppendLine($"Total violations: {TotalViolations}");

        if (TotalViolations > 0)
        {
            sb.AppendLine();
            sb.AppendLine("See https://aka.ms/azsdk/codeowners to learn how to fix these violations");
        }

        return sb.ToString().TrimEnd();
    }

    private static string Colorize(string value) =>
        Environment.GetEnvironmentVariable("NO_COLOR") == null ? $"{Red}{value}{Reset}" : value;
}
