// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Pipeline;

/// <summary>
/// Aggregate counts for a Copilot pipeline-fix evaluation run, one field per <see cref="EvaluationOutcome"/>
/// plus rollups. Provided so dashboard/telemetry queries can read a single pre-computed object instead of
/// re-aggregating the per-PR <c>results</c> array. Every count is always present (defaults to 0).
/// </summary>
public class EvaluationSummary
{
    /// <summary>Total evaluated FAILURE -> SUCCESS windows (i.e. the number of results).</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>Count of <see cref="EvaluationOutcome.PipelineFixSuccess"/> results.</summary>
    [JsonPropertyName("pipelineFixSuccess")]
    public int PipelineFixSuccess { get; set; }

    /// <summary>Count of <see cref="EvaluationOutcome.ModelJudgedSuccess"/> results.</summary>
    [JsonPropertyName("modelJudgedSuccess")]
    public int ModelJudgedSuccess { get; set; }

    /// <summary>Count of <see cref="EvaluationOutcome.ModelJudgedFailure"/> results.</summary>
    [JsonPropertyName("modelJudgedFailure")]
    public int ModelJudgedFailure { get; set; }

    /// <summary>Count of <see cref="EvaluationOutcome.Skipped"/> results.</summary>
    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }

    /// <summary>Count of <see cref="EvaluationOutcome.ModelError"/> results.</summary>
    [JsonPropertyName("modelError")]
    public int ModelError { get; set; }

    /// <summary>
    /// Fixes counted as successful: deterministic (<see cref="EvaluationOutcome.PipelineFixSuccess"/>) plus
    /// model-judged (<see cref="EvaluationOutcome.ModelJudgedSuccess"/>). The headline "how many agent fixes
    /// worked" metric.
    /// </summary>
    [JsonPropertyName("successCount")]
    public int SuccessCount => PipelineFixSuccess + ModelJudgedSuccess;

    /// <summary>Builds a summary by counting outcomes across the given results.</summary>
    public static EvaluationSummary FromResults(IEnumerable<EvaluationResult> results)
    {
        var summary = new EvaluationSummary();
        foreach (var result in results)
        {
            summary.Total++;
            switch (result.Outcome)
            {
                case EvaluationOutcome.PipelineFixSuccess:
                    summary.PipelineFixSuccess++;
                    break;
                case EvaluationOutcome.ModelJudgedSuccess:
                    summary.ModelJudgedSuccess++;
                    break;
                case EvaluationOutcome.ModelJudgedFailure:
                    summary.ModelJudgedFailure++;
                    break;
                case EvaluationOutcome.Skipped:
                    summary.Skipped++;
                    break;
                case EvaluationOutcome.ModelError:
                    summary.ModelError++;
                    break;
            }
        }
        return summary;
    }
}
