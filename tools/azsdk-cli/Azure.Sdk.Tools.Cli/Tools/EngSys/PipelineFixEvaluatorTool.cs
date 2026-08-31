// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers.EngSys;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.EngSys;

[Description("Evaluates whether GitHub Copilot's fixes for failing Azure SDK pipelines changed failing checks to passing, recording trendable metrics.")]
public class PipelineFixEvaluatorTool(
    IPipelineFixEvaluatorHelper pipelineFixEvaluatorHelper,
    ILogger<PipelineFixEvaluatorTool> logger
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.EngSys];

    private const int DefaultSinceDays = 1;
    private readonly Argument<string> ownerArg = new("owner")
    {
        Description = "GitHub repository owner (e.g. Azure)",
    };

    private readonly Argument<string> repoArg = new("repo")
    {
        Description = "GitHub repository name (e.g. azure-sdk-for-python)",
    };

    private readonly Option<int> sinceDaysOpt = new("--since-days")
    {
        Description = "Look back this many days for merged PRs",
        DefaultValueFactory = _ => DefaultSinceDays,
    };

    private readonly Option<DateTimeOffset?> untilOpt = new("--until")
    {
        Description = "End of the evaluation window as an ISO-8601 timestamp (defaults to the current time)",
        Required = false,
    };

    protected override Command GetCommand() =>
        new Command("evaluate", "Evaluate whether Copilot's pipeline-failure fixes changed failing checks to passing over the last N days")
        { ownerArg, repoArg, sinceDaysOpt, untilOpt };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var owner = parseResult.GetValue(ownerArg)!;
        var repo = parseResult.GetValue(repoArg)!;
        var sinceDays = parseResult.GetValue(sinceDaysOpt);
        var until = parseResult.GetValue(untilOpt);
        return await EvaluatePipelineFixes(owner, repo, sinceDays, until, ct);
    }

    public async Task<PipelineFixEvaluatorResponse> EvaluatePipelineFixes(
        [Description("GitHub repository owner (e.g. Azure)")] string owner,
        [Description("GitHub repository name (e.g. azure-sdk-for-python)")] string repo,
        [Description("Look back this many days for merged PRs (default 1)")] int sinceDays = DefaultSinceDays,
        [Description("End of the evaluation window as an ISO-8601 timestamp (defaults to the current time)")] DateTimeOffset? until = null,
        CancellationToken ct = default)
    {
        try
        {
            if (sinceDays <= 0)
            {
                return new PipelineFixEvaluatorResponse
                {
                    ResponseError = $"--since-days must be greater than zero (got {sinceDays})."
                };
            }

            logger.LogDebug("Evaluating Copilot pipeline fixes in {Owner}/{Repo} for merged PRs over the last {SinceDays} days", owner, repo, sinceDays);

            // Calendar-day keys must always represent stable midnight-to-midnight UTC windows.
            var requestedEnd = (until ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var windowEnd = new DateTimeOffset(requestedEnd.UtcDateTime.Date, TimeSpan.Zero).AddDays(1);
            List<PipelineFixDateEvaluation> dates = [];
            for (var dayOffset = 0; dayOffset < sinceDays; dayOffset++)
            {
                var dayUntil = windowEnd.AddDays(-dayOffset);
                var daySince = dayUntil.AddDays(-1);
                var evaluations = await pipelineFixEvaluatorHelper.EvaluatePipelineFixesAsync(
                    owner, repo, daySince, dayUntil, ct);
                dates.Add(new PipelineFixDateEvaluation
                {
                    Date = DateOnly.FromDateTime(daySince.UtcDateTime),
                    Evaluations = evaluations,
                });
            }

            return new PipelineFixEvaluatorResponse
            {
                Owner = owner,
                Repo = repo,
                Dates = dates,
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to evaluate Copilot pipeline fixes for {Owner}/{Repo}", owner, repo);
            return new PipelineFixEvaluatorResponse
            {
                ResponseError = $"Failed to evaluate Copilot pipeline fixes for {owner}/{repo}: {ex.Message}"
            };
        }
    }
}
