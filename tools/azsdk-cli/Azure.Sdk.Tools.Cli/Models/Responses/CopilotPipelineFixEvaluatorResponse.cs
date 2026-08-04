// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Pipeline;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Response for a Copilot pipeline-fix evaluation run: one result per Copilot fix attempt on a merged pull
/// request where a check that ran on both sides changed state. A pull request can appear more than once -
/// see CopilotPipelineFixResult for how attempts are told apart. Consumers that need
/// aggregate counts derive them from <see cref="Results"/>.
/// </summary>
public class CopilotPipelineFixEvaluatorResponse : CommandResponse
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

    [JsonPropertyName("model_used")]
    public string ModelUsed { get; set; } = string.Empty;

    [JsonPropertyName("results")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CopilotPipelineFixResult>? Results { get; init; }

    private const int PrNumberWidth = 6;
    private const int FixPrWidth = 6;
    private const int TitleWidth = 40;
    // Width of the longest pipeline-outcome name, CopilotPipelineFixSuccess (25 chars).
    private const int PipelineWidth = 25;
    // Width of the longest verification name, CopilotJudgeVerifiedFailure (27 chars).
    private const int VerificationWidth = 27;

    protected override string Format()
    {
        var results = Results ?? [];
        if (results.Count == 0)
        {
            return "No Copilot pipeline fixes were found to evaluate in this window.";
        }

        var output = new StringBuilder();

        // The fix pull request is shown because one pull request can have several fix attempts, which would
        // otherwise be indistinguishable rows repeating the same number.
        output.AppendLine(Row("PR#", "Fix PR", "PR Title", "Pipeline Outcome", "Fix Survival"));
        output.AppendLine(Row(
            new string('-', PrNumberWidth),
            new string('-', FixPrWidth),
            new string('-', TitleWidth),
            new string('-', PipelineWidth),
            new string('-', VerificationWidth)));

        foreach (var r in results)
        {
            output.AppendLine(Row(
                r.PrNumber.ToString(CultureInfo.InvariantCulture),
                r.FixPrNumber?.ToString(CultureInfo.InvariantCulture) ?? "-",
                Trim(r.PrTitle, TitleWidth),
                r.PipelineOutcome.ToString(),
                r.Verification.ToString()));
        }

        return output.ToString();
    }

    private static string Row(string prNumber, string fixPrNumber, string title, string pipeline, string verification) =>
        $"| {prNumber,-PrNumberWidth} | {fixPrNumber,-FixPrWidth} | {title,-TitleWidth} | {pipeline,-PipelineWidth} | {verification,-VerificationWidth} |";

    private static string Trim(string? value, int width)
    {
        value ??= string.Empty;
        return value.Length <= width ? value : value[..(width - 1)] + "…";
    }
}
