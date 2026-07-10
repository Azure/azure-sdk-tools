// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Pipeline;

namespace Azure.Sdk.Tools.Cli.Models.Responses;

/// <summary>
/// Response for a Copilot pipeline-fix evaluation run: one result per merged PR where Copilot was
/// asked to fix a failing pipeline, plus a short aggregate summary.
/// </summary>
public class PipelineFixEvaluatorResponse : CommandResponse
{
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    [JsonPropertyName("repo")]
    public string Repo { get; set; } = string.Empty;

    [JsonPropertyName("since")]
    public DateTimeOffset Since { get; set; }

    [JsonPropertyName("until")]
    public DateTimeOffset Until { get; set; }

    [JsonPropertyName("results")]
    public IReadOnlyList<EvaluationResult> Results { get; set; } = [];

    /// <summary>
    /// Aggregate outcome counts for this run, derived from <see cref="Results"/>. Emitted so
    /// dashboard/telemetry queries can read pre-computed totals instead of re-aggregating the array.
    /// </summary>
    [JsonPropertyName("summary")]
    public EvaluationSummary Summary => EvaluationSummary.FromResults(Results);

    private const int PrNumberWidth = 6;
    private const int TitleWidth = 45;
    private const int OutcomeWidth = 18;  // longest outcome: ModelJudgedSuccess

    protected override string Format()
    {
        var output = new StringBuilder();

        output.AppendLine(Row("PR#", "PR Title", "Evaluation Outcome"));
        output.AppendLine(Row(
            new string('-', PrNumberWidth),
            new string('-', TitleWidth),
            new string('-', OutcomeWidth)));

        foreach (var r in Results)
        {
            output.AppendLine(Row(
                r.PRNumber.ToString(),
                Trim(r.PRTitle, TitleWidth),
                r.Outcome.ToString()));
        }

        if (Results.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("Reasons:");
            foreach (var r in Results)
            {
                output.AppendLine(
                    $"  #{r.PRNumber} (build {r.FailedBuildId} -> {r.SucceededBuildId}) [{r.Outcome}]: {r.Reason}");
            }

            var s = Summary;
            output.AppendLine();
            output.AppendLine(
                $"Summary: {s.Total} evaluated | {s.SuccessCount} succeeded " +
                $"(PipelineFixSuccess={s.PipelineFixSuccess}, ModelJudgedSuccess={s.ModelJudgedSuccess}) | " +
                $"ModelJudgedFailure={s.ModelJudgedFailure}, Skipped={s.Skipped}, ModelError={s.ModelError}");
        }

        return output.ToString();
    }

    private static string Row(string prNumber, string title, string outcome) =>
        $"| {prNumber,-PrNumberWidth} | {title,-TitleWidth} | {outcome,-OutcomeWidth} |";

    private static string Trim(string value, int width)
    {
        value ??= string.Empty;
        return value.Length <= width ? value : value[..(width - 3)] + "...";
    }
}
