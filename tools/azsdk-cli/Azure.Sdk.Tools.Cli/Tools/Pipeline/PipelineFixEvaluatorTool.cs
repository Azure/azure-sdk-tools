// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers.Pipeline;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Tools.Core;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.Pipeline;

/// <summary>
/// Evaluates whether GitHub Copilot's fixes for Azure Pipelines failures actually worked.
/// Fetches merged PRs where a human asked Copilot to fix a failing pipeline, and decides
/// whether Copilot's commits took a pipeline from FAILURE to SUCCESS and survived into the final
/// merged code.
/// </summary>
[Description("Evaluates whether GitHub Copilot's fixes for failing Azure SDK pipelines took the pipeline from failure to success and survived into the merged pull request, recording trendable metrics.")]
[McpServerToolType]
public class PipelineFixEvaluatorTool(
    IPipelineFixEvaluator evaluator,
    ILogger<PipelineFixEvaluatorTool> logger
) : MCPTool
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.AzurePipelines];

    private const string EvaluatePipelineFixesToolName = "azsdk_evaluate_pipeline_fixes";

    private readonly Argument<string> ownerArg = new("owner")
    {
        Description = "GitHub repository owner (e.g. Azure)",
    };

    private readonly Argument<string> repoArg = new("repo")
    {
        Description = "GitHub repository name (e.g. azure-sdk-for-net)",
    };

    private readonly Option<int> sinceDaysOpt = new("--since-days")
    {
        Description = "Look back this many days for merged PRs",
        DefaultValueFactory = _ => 1,
    };

    private readonly Option<string> modelOpt = new("--model")
    {
        Description = "Chat model used by the model-judged tier",
        DefaultValueFactory = _ => "claude-sonnet-4.5",
    };

    private readonly Option<bool> dryRunOpt = new("--dry-run")
    {
        Description = "Run deterministic tiers and emit metrics but never call the model tier",
    };

    protected override Command GetCommand() =>
        new McpCommand("evaluate", "Evaluate whether Copilot's pipeline-failure fixes worked and survived into merged PRs over the last N days", EvaluatePipelineFixesToolName)
        { ownerArg, repoArg, sinceDaysOpt, modelOpt, dryRunOpt };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var owner = parseResult.GetValue(ownerArg)!;
        var repo = parseResult.GetValue(repoArg)!;
        var sinceDays = parseResult.GetValue(sinceDaysOpt);
        var model = parseResult.GetValue(modelOpt)!;
        var dryRun = parseResult.GetValue(dryRunOpt);
        return await EvaluatePipelineFixes(owner, repo, sinceDays, model, dryRun, ct);
    }

    [McpServerTool(Name = EvaluatePipelineFixesToolName), Description("Evaluate whether GitHub Copilot's fixes for failing pipelines took the pipeline from failure to success and survived into merged PRs over the last N days, and record trendable metrics")]
    public async Task<PipelineFixEvaluatorResponse> EvaluatePipelineFixes(
        [Description("GitHub repository owner (e.g. Azure)")] string owner,
        [Description("GitHub repository name (e.g. azure-sdk-for-net)")] string repo,
        [Description("Look back this many days for merged PRs (default 1)")] int sinceDays = 1,
        [Description("Chat model used by the model-judged tier (default claude-sonnet-4.5)")] string model = "claude-sonnet-4.5",
        [Description("Run deterministic tiers and emit metrics but never call the model tier")] bool dryRun = false,
        CancellationToken ct = default)
    {
        try
        {
            var until = DateTimeOffset.UtcNow;
            var since = until - TimeSpan.FromDays(sinceDays);

            logger.LogDebug("Evaluating Copilot pipeline fixes in {Owner}/{Repo} for merged PRs since {Since}", owner, repo, since);

            var results = await evaluator.EvaluateAsync(
                owner, repo, since, until, model, dryRun, ct);

            logger.LogDebug("Evaluated {Count} Copilot pipeline-fix PR(s) in {Owner}/{Repo}", results.Count, owner, repo);

            return new PipelineFixEvaluatorResponse
            {
                Owner = owner,
                Repo = repo,
                Since = since,
                Until = until,
                Results = results,
            };
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
