// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Pipeline;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Response for a Copilot pipeline-fix evaluation run. A result records either an adopted fix with a check
/// transition or a workflow branch that was never used. A pull request can appear more than once - see
/// CopilotPipelineFixResult for how attempts are told apart. Consumers derive aggregate counts from
/// <see cref="Results"/>.
/// </summary>
public class PipelineFixEvaluatorResponse : CommandResponse
{
    [JsonPropertyName("owner")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Owner { get; set; }

    [JsonPropertyName("repo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; set; }

    [JsonPropertyName("since")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Since { get; set; }

    [JsonPropertyName("until")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Until { get; set; }

    [JsonPropertyName("results")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CopilotPipelineFixResult>? Results { get; init; }

    private const int PrNumberWidth = 6;
    private const int FixBranchWidth = 48;
    private const int TitleWidth = 40;
    // Width of the longest pipeline-outcome name, CopilotPipelineFixSuccess (25 chars).
    private const int PipelineWidth = 25;
    // Width of the longest verification name, CopilotFixNotMerged (20 chars).
    private const int VerificationWidth = 20;

    protected override string Format()
    {
        var results = Results ?? [];
        if (results.Count == 0)
        {
            return "No Copilot pipeline fixes were found to evaluate in this window.";
        }

        var output = new StringBuilder();

        output.AppendLine(Row("PR#", "Fix Branch", "PR Title", "Pipeline Outcome", "Verification"));
        output.AppendLine(Row(
            new string('-', PrNumberWidth),
            new string('-', FixBranchWidth),
            new string('-', TitleWidth),
            new string('-', PipelineWidth),
            new string('-', VerificationWidth)));

        foreach (var r in results)
        {
            output.AppendLine(Row(
                r.PrNumber.ToString(CultureInfo.InvariantCulture),
                Trim(r.FixBranch, FixBranchWidth) is { Length: > 0 } branch ? branch : "-",
                Trim(r.PrTitle, TitleWidth),
                r.PipelineOutcome?.ToString() ?? "-",
                r.Verification.ToString()));
        }

        return output.ToString();
    }

    private static string Row(string prNumber, string fixBranch, string title, string pipeline, string verification) =>
        $"| {prNumber,-PrNumberWidth} | {fixBranch,-FixBranchWidth} | {title,-TitleWidth} | {pipeline,-PipelineWidth} | {verification,-VerificationWidth} |";

    private static string Trim(string? value, int width)
    {
        value ??= string.Empty;
        return value.Length <= width ? value : value[..(width - 1)] + "…";
    }
}
