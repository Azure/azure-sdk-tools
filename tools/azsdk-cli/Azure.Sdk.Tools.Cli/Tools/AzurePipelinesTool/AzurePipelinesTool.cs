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

namespace Azure.Sdk.Tools.Cli.Tools.AzurePipelinesTool;

[McpServerToolType, Description("Fetches data from Azure Pipelines")]
public class AzurePipelinesTool(
    IAzureService azureService,
    IAzureAgentServiceFactory azureAgentServiceFactory,
    IOutputService output,
    ILogger<AzurePipelinesTool> logger) : MCPTool
{
    public string? project;

    private BuildHttpClient buildClient;
    private TestResultsHttpClient testClient;
    private readonly Boolean initialized = false;

    // Commands
    private readonly string getPipelineRunCommandName = "get-pipeline-run";
    private readonly string analyzePipelineCommandName = "analyze";

    // Options
    private readonly Option<int> buildIdOpt = new(["--build-id", "-b"], "Pipeline/Build ID") { IsRequired = true };
    private readonly Option<int> logIdOpt = new(["--log-id"], "ID of the pipeline task log") { IsRequired = true };
    private readonly Option<string> projectOpt = new(["--project", "-p"], () => "public", "Pipeline project name");
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
            logger.LogInformation("Getting pipeline run {buildId} in project {project}...", buildId, project);
            var result = await GetPipelineRun(buildId, project);
            ctx.ExitCode = ExitCode;
            output.Output(result);
        }
        else if (cmd == analyzePipelineCommandName)
        {
            logger.LogInformation("Analyzing pipeline {buildId} in project {project}...", buildId, project);
            var logId = ctx.ParseResult.GetValueForOption(logIdOpt);
            var aiEndpoint = ctx.ParseResult.GetValueForOption(aiEndpointOpt);
            var aiModel = ctx.ParseResult.GetValueForOption(aiModelOpt);
            var result = await AnalyzePipelineFailureLog(buildId, logId, project, aiEndpoint, aiModel);
            ctx.ExitCode = ExitCode;
            output.Output(result);
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
    public async Task<Build> GetPipelineRun(int buildId, string? _project = null)
    {
        // _project state changes to the last successful GET to the build api
        project ??= project ?? "public";

        try
        {
            var build = await buildClient.GetBuildAsync(_project, buildId);
            project = _project;
            return build;
        }
        catch { }

        try
        {
            _project = _project == "public" ? "internal" : "public";
            var build = await buildClient.GetBuildAsync(_project, buildId);
            project = _project;
            return build;
        }
        catch { }

        throw new Exception($"Failed to find build {buildId} in project 'public' or 'internal'");
    }

    [McpServerTool, Description("Gets failures from non-test steps in a pipeline run")]
    public async Task<List<TimelineRecord>?> GetPipelineFailures(int buildId)
    {
        var failedNonTests = await GetPipelineFailuresTyped(buildId);
        return failedNonTests;
    }

    public async Task<List<TimelineRecord>> GetPipelineFailuresTyped(int buildId)
    {
        var timeline = await buildClient.GetBuildTimelineAsync(project, buildId);
        var failedNonTests = timeline.Records.Where(r => r.Result == TaskResult.Failed && !IsTestStep(r.Name)).ToList();
        return failedNonTests;
    }

    [McpServerTool, Description(@"
        Analyze and diagnose the failed test results from a pipeline.
        Include relevant data like test name and environment, error type, error messages, functions and error lines.
        Provide suggested next steps.
    ")]
    public async Task<List<object>> GetPipelineFailedTestResults(int buildId)
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

        var failedRunData = new List<object>();

        foreach (var runId in failedRuns)
        {
            var testCases = await testClient.GetTestResultsAsync(
                            project, runId, outcomes: [TestOutcome.Failed, TestOutcome.Aborted]);

            foreach (var tc in testCases)
            {
                failedRunData.Add(new
                {
                    RunId = runId,
                    tc.TestCaseTitle,
                    tc.ErrorMessage,
                    tc.StackTrace,
                    tc.Outcome,
                    tc.Url
                });
            }
        }

        return failedRunData;
    }

    [McpServerTool, Description(@"
        Get the failed steps from a pipeline that are not test steps. Show detailed information/logs when available.
        Find other log lines in addition to the final error that may be descriptive of the problem.
        For example, 'Powershell exited with code 1' is not an error message, but the error message may be in the logs above it.
    ")]
    public async Task<List<string>> GetPipelineFailureLog(int buildId, int logId, string? project = null)
    {
        project ??= project ?? "public";
        var logContent = await buildClient.GetBuildLogLinesAsync(project, buildId, logId);
        var output = new List<string>();
        foreach (var line in logContent)
        {
            output.Add(line);
        }
        return output;
    }

    public async Task<string> AnalyzePipelineFailureLog(int buildId, int logId, string? project = null, string? aiEndpoint = null, string? aiModel = null)
    {
        project ??= project ?? "public";
        var aiAgentService = azureAgentServiceFactory.Create(aiModel, aiEndpoint);

        var logContent = await buildClient.GetBuildLogLinesAsync(project, buildId, logId);
        var logText = string.Join("\n", logContent);
        var logBytes = System.Text.Encoding.UTF8.GetBytes(logText);
        var session = $"{project}-{buildId}-{logId}";
        var filename = $"{session}.txt";

        using var stream = new MemoryStream(logBytes);
        var (response, usage) = await aiAgentService.QueryFileAsync(stream, filename, session, "Why did this pipeline fail?");
        usage.LogCost();

        // Sometimes chat gpt likes to wrap the json in markdown
        if (response.StartsWith("```json")
            && response.EndsWith("```"))
        {
            response = response[7..^3].Trim();
        }

        try
            {
                return output.ValidateAndFormat<LogAnalysisResponse>(response);
            }
            catch (JsonException ex)
            {
                logger.LogError("Failed to deserialize log analysis response: {exception}", ex.Message);
                logger.LogError("Response:\n{response}", response);
                SetFailure();
                return "Failed to deserialize log analysis response. Check the logs for more details.";
            }
    }

    [McpServerTool, Description("Analyze and diagnose the failed test results from a pipeline")]
    public async Task<string> AnalyzePipelineFailureLog(int buildId, int logId, string? project = null)
    {
        return await AnalyzePipelineFailureLog(buildId, logId, project, null, null);
    }

    public bool IsTestStep(string stepName)
    {
        return stepName.Contains("test", StringComparison.OrdinalIgnoreCase);
    }
}
