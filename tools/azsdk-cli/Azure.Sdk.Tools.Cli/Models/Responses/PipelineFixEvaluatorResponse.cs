// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.Pipeline;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Response for automated pipeline-fix evaluations grouped into one-day windows.
/// </summary>
public class PipelineFixEvaluatorResponse : CommandResponse
{
    [JsonPropertyName("owner")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Owner { get; set; }

    [JsonPropertyName("repo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; set; }

    [JsonPropertyName("dates")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PipelineFixDateEvaluation>? Dates { get; init; }

    private const int PrNumberWidth = 6;
    private const int DateWidth = 10;
    private const int TitleWidth = 40;
    // Width of the longest pipeline-outcome name, CopilotPipelineFixSuccess (25 chars).
    private const int PipelineWidth = 25;
    // Width of the longest verification name, CopilotFixNotMerged (20 chars).
    private const int VerificationWidth = 20;

    protected override string Format()
    {
        var results = Dates?.SelectMany(date => date.Evaluations).ToList() ?? [];
        if (results.Count == 0)
        {
            return "No Copilot pipeline fixes were found to evaluate in this window.";
        }

        var output = new StringBuilder();

        output.AppendLine(Row("Date", "PR#", "PR Title", "Pipeline Outcome", "Verification"));
        output.AppendLine(Row(
            new string('-', DateWidth),
            new string('-', PrNumberWidth),
            new string('-', TitleWidth),
            new string('-', PipelineWidth),
            new string('-', VerificationWidth)));

        foreach (var date in Dates ?? [])
        {
            foreach (var result in date.Evaluations)
            {
                output.AppendLine(Row(
                    date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    result.PrNumber.ToString(CultureInfo.InvariantCulture),
                    Trim(result.PrTitle, TitleWidth),
                    result.PipelineOutcome?.ToString() ?? "-",
                    result.Verification.ToString()));
            }
        }

        return output.ToString();
    }

    private static string Row(string date, string prNumber, string title, string pipeline, string verification) =>
        $"| {date,-DateWidth} | {prNumber,-PrNumberWidth} | {title,-TitleWidth} | {pipeline,-PipelineWidth} | {verification,-VerificationWidth} |";

    private static string Trim(string? value, int width)
    {
        value ??= string.Empty;
        return value.Length <= width ? value : value[..(width - 1)] + "…";
    }
}
