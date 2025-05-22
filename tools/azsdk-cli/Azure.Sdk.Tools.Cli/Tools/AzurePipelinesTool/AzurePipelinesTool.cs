// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Microsoft.VisualStudio.Services.OAuth;
using Azure.Core;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tools.AzurePipelinesTool;

[McpServerToolType, Description("Fetches data from Azure Pipelines")]
public class AzurePipelinesTool(
    IAzureService azureService,
    IAzureAgentServiceFactory azureAgentServiceFactory,
    IOutputService output,
    ILogger<AzurePipelinesTool> logger) : MCPTool
{
    private BuildHttpClient buildClient;
    private TestResultsHttpClient testClient;
    private IAzureAgentService azureAgentService;
    private TokenUsageHelper usage;
    private readonly Boolean initialized = false;

    // Commands
    private readonly string getPipelineRunCommandName = "pipeline";
    private readonly string analyzePipelineCommandName = "analyze";

    // Options
    private readonly Option<int> buildIdOpt = new(["--build-id", "-b"], "Pipeline/Build ID") { IsRequired = true };
    private readonly Option<int> logIdOpt = new(["--log-id"], "ID of the pipeline task log");
    private readonly Option<string> projectOpt = new(["--project", "-p"], "Pipeline project name");
    private readonly Option<string> aiEndpointOpt = new(["--ai-endpoint"], "The endpoint for the Azure AI Agent service");
    private readonly Option<string> aiModelOpt = new(["--ai-model"], "The model to use for the Azure AI Agent");

    public override Command GetCommand()
    {
        Command command = new("azp", "Azure Pipelines Tool");
        var pipelineRunCommand = new Command(getPipelineRunCommandName, "Get details for a pipeline run") { buildIdOpt, projectOpt };
        var analyzePipelineCommand = new Command(analyzePipelineCommandName, "Analyze a pipeline run") {
            buildIdOpt, projectOpt, logIdOpt, aiEndpointOpt, aiModelOpt
        };

        // Do not add a handler for the 'azp' command, that way System.CommandLine can fall back to the
        // root command handler and print help text.
        foreach (var subCommand in new[] { pipelineRunCommand, analyzePipelineCommand })
        {
            subCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            command.AddCommand(subCommand);
        }

        return command;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        Initialize();

        var cmd = ctx.ParseResult.CommandResult.Command.Name;
        var buildId = ctx.ParseResult.GetValueForOption(buildIdOpt);
        var project = ctx.ParseResult.GetValueForOption(projectOpt);

        if (cmd == getPipelineRunCommandName)
        {
            logger.LogInformation("Getting pipeline run {buildId}...", buildId);
            var result = await GetPipelineRun(project, buildId);
            ctx.ExitCode = ExitCode;
            output.Output(result);
        }
        else if (cmd == analyzePipelineCommandName)
        {
            var logId = ctx.ParseResult.GetValueForOption(logIdOpt);
            var aiEndpoint = ctx.ParseResult.GetValueForOption(aiEndpointOpt);
            var aiModel = ctx.ParseResult.GetValueForOption(aiModelOpt);

            logger.LogInformation("Analyzing pipeline {buildId}...", buildId);
            azureAgentService = azureAgentServiceFactory.Create(aiModel, aiEndpoint);

            if (logId != 0)
            {
                var result = await AnalyzePipelineFailureLog(project, buildId, logId);
                ctx.ExitCode = ExitCode;
                usage?.LogCost();
                output.Output(result);
            }
            else
            {
                var result = await AnalyzePipeline(project, buildId);
                ctx.ExitCode = ExitCode;
                usage?.LogCost();
                output.Output(result);
            }
        }
        else
        {
            logger.LogError("Command {cmd} not implemented", cmd);
            SetFailure();
        }
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }
        var tokenScope = new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" };  // Azure DevOps scope
        var token = azureService.GetCredential().GetToken(new TokenRequestContext(tokenScope));
        var tokenCredential = new VssOAuthAccessTokenCredential(token.Token);
        var connection = new VssConnection(new Uri($"https://dev.azure.com/azure-sdk"), tokenCredential);
        buildClient = connection.GetClient<BuildHttpClient>();
        testClient = connection.GetClient<TestResultsHttpClient>();
    }

    [McpServerTool, Description("Gets details for a pipeline run")]
    public async Task<Build> GetPipelineRun(string? project, int buildId)
    {
        try
        {
            var build = await buildClient.GetBuildAsync(project ?? "public", buildId);
            return build;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(project))
            {
                throw new Exception($"Failed to find build {buildId} in project 'public': {ex.Message}");
            }
            // If project is not specified, try both azure sdk public and internal devops projects
            return await GetPipelineRun("internal", buildId);
        }
    }

    [McpServerTool, Description("Gets failures from tasks (non-test failures) in a pipeline run")]
    public async Task<List<TimelineRecord>?> GetPipelineTaskFailures(string project, int buildId)
    {
        var timeline = await buildClient.GetBuildTimelineAsync(project, buildId);
        var failedNonTests = timeline.Records.Where(
                                r => r.Result == TaskResult.Failed
                                && r.RecordType == "Task"
                                && !IsTestStep(r.Name))
                            .ToList();
        return failedNonTests;
    }

    [McpServerTool, Description(@"
        Analyze and diagnose the failed test results from a pipeline.
        Include relevant data like test name and environment, error type, error messages, functions and error lines.
        Provide suggested next steps.
    ")]
    public async Task<List<FailedTestRunResponse>> GetPipelineFailedTestResults(string project, int buildId)
    {
        var results = new List<ShallowTestCaseResult>();
        var testRuns = await testClient.GetTestResultsByPipelineAsync(project, buildId);
        results.AddRange(testRuns);
        while (testRuns.ContinuationToken != null)
        {
            var nextResults = await testClient.GetTestResultsByPipelineAsync(project, buildId, continuationToken: testRuns.ContinuationToken);
            results.AddRange(nextResults);
            testRuns.ContinuationToken = nextResults.ContinuationToken;
        }

        var failedRuns = results.Where(
            r => r.Outcome == TestOutcome.Failed.ToString()
            || r.Outcome == TestOutcome.Aborted.ToString())
        .Select(r => r.RunId)
        .Distinct()
        .ToList();

        var failedRunData = new List<FailedTestRunResponse>();

        foreach (var runId in failedRuns)
        {
            var testCases = await testClient.GetTestResultsAsync(
                            project, runId, outcomes: [TestOutcome.Failed, TestOutcome.Aborted]);

            foreach (var tc in testCases)
            {
                failedRunData.Add(new FailedTestRunResponse
                {
                    RunId = runId,
                    TestCaseTitle = tc.TestCaseTitle,
                    ErrorMessage = tc.ErrorMessage,
                    StackTrace = tc.StackTrace,
                    Outcome = tc.Outcome,
                    Url = tc.Url
                });
            }
        }

        return failedRunData;
    }

    [McpServerTool, Description("Analyze and diagnose the failed test results from a pipeline")]
    public async Task<LogAnalysisResponse> AnalyzePipelineFailureLog(string project, int buildId, int logId)
    {
        project ??= project ?? "public";

        var logContent = await buildClient.GetBuildLogLinesAsync(project, buildId, logId);
        var logText = string.Join("\n", logContent);
        var logBytes = System.Text.Encoding.UTF8.GetBytes(logText);
        var session = $"{project}-{buildId}-{logId}";
        var filename = $"{session}.txt";

        using var stream = new MemoryStream(logBytes);
        var (result, _usage) = await azureAgentService.QueryFileAsync(stream, filename, session, "Why did this pipeline fail?");
        if (usage != null)
        {
            usage += _usage;
        }
        else
        {
            usage = _usage;
        }

        // Sometimes chat gpt likes to wrap the json in markdown
        if (result.StartsWith("```json")
            && result.EndsWith("```"))
        {
            result = result[7..^3].Trim();
        }

        try
        {
            return JsonSerializer.Deserialize<LogAnalysisResponse>(result);
        }
        catch (JsonException ex)
        {
            logger.LogError("Failed to deserialize log analysis response: {exception}", ex.Message);
            logger.LogError("Response:\n{result}", result);

            SetFailure();

            return new LogAnalysisResponse()
            {
                ResponseError = "Failed to deserialize log analysis response. Check the logs for more details.",
            };
        }
    }

    [McpServerTool, Description("Analyze azure pipeline for failures")]
    public async Task<AnalyzePipelineResponse> AnalyzePipeline(string? project, int buildId)
    {
        if (string.IsNullOrEmpty(project))
        {
            var pipeline = await GetPipelineRun(project, buildId);
            project = pipeline.Project.Name;
        }
        var failedTasks = await GetPipelineTaskFailures(project, buildId);
        var failedTests = await GetPipelineFailedTestResults(project, buildId);

        var taskAnalysis = new List<LogAnalysisResponse>();

        foreach (var task in failedTasks ?? [])
        {
            if (task.Log == null)
            {
                continue;
            }
            var analysis = await AnalyzePipelineFailureLog(project, buildId, task.Log.Id);
            taskAnalysis.Add(analysis);
        }

        return new AnalyzePipelineResponse()
        {
            FailedTasks = taskAnalysis,
            FailedTests = failedTests
        };
    }

    public bool IsTestStep(string stepName)
    {
        return stepName.Contains("test", StringComparison.OrdinalIgnoreCase);
    }
}
