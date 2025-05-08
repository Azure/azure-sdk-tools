using ModelContextProtocol.Server;
using System.ComponentModel;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.VisualStudio.Services.OAuth;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tools.AzurePipelinesTool;

[McpServerToolType, Description("Fetches data from Azure Pipelines")]
public class AzurePipelinesTool(IAzureService azureService, IAIAgentService aiAgentService, ILogger<AzurePipelinesTool> logger) : MCPTool
{
    public string? project;

    private BuildHttpClient? buildClient;
    private TestResultsHttpClient? testClient;
    private readonly IAzureService azureService = azureService;
    private readonly IAIAgentService aiAgentService = aiAgentService;
    private readonly ILogger<AzurePipelinesTool> logger = logger;
    private readonly Boolean initialized = false;

    // Commands
    private readonly string getPipelineRunCommandName = "get-pipeline-run";
    private readonly string analyzePipelineCommandName = "analyze";

    // Options
    private readonly Option<int> buildIdOpt = new(["--build-id", "-b"], "Pipeline/Build ID") { IsRequired = true };
    private readonly Option<int> logIdOpt = new(["--log-id"], "ID of the pipeline task log") { IsRequired = true };
    private readonly Option<string> projectOpt = new(["--project", "-p"], () => "public", "Pipeline project name");

    public override Command GetCommand()
    {
        Command command = new("azp", "Azure Pipelines Tool");

        var pipelineRunCommand = new Command(this.getPipelineRunCommandName, "Get details for a pipeline run") { this.buildIdOpt, this.projectOpt };
        var analyzePipelineCommand = new Command(this.analyzePipelineCommandName, "Analyze a pipeline run") { this.buildIdOpt, this.projectOpt, this.logIdOpt };

        // Do not add a handler for the 'azp' command, that way System.CommandLine can fall back to the
        // root command handler and print help text.
        foreach (var subCommand in new[] { pipelineRunCommand, analyzePipelineCommand })
        {
            subCommand.SetHandler(async ctx => { ctx.ExitCode = await HandleCommand(ctx, ctx.GetCancellationToken()); });
            command.AddCommand(subCommand);
        }

        return command;
    }

    public override async Task<int> HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        Initialize();

        var cmd = ctx.ParseResult.CommandResult.Command.Name;
        var buildId = ctx.ParseResult.GetValueForOption(this.buildIdOpt);
        var project = ctx.ParseResult.GetValueForOption(this.projectOpt);

        if (cmd == this.getPipelineRunCommandName)
        {
            this.logger.LogInformation("Getting pipeline run {buildId} in project {project}", buildId, project);
            var result = await GetPipelineRun(buildId, project);
            this.logger.LogInformation("{result}", result);
            return 0;
        }
        else if (cmd == this.analyzePipelineCommandName)
        {
            this.logger.LogInformation("Analyzing pipeline {buildId} in project {project}", buildId, project);
            var logId = ctx.ParseResult.GetValueForOption(this.logIdOpt);
            var result = await AnalyzePipelineFailureLog(buildId, logId, project);
            this.logger.LogInformation("{result}", result);
            return 0;
        }

        this.logger.LogError("Command {cmd} not implemented", cmd);
        return 1;
    }

    private void Initialize()
    {
        if (this.initialized)
        {
            return;
        }
        var tokenScope = new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" };  // Azure DevOps scope
        var token = this.azureService.GetCredential().GetToken(new TokenRequestContext(tokenScope));
        var tokenCredential = new VssOAuthAccessTokenCredential(token.Token);
        var connection = new VssConnection(new Uri($"https://dev.azure.com/azure-sdk"), tokenCredential);
        this.buildClient = connection.GetClient<BuildHttpClient>();
        this.testClient = connection.GetClient<TestResultsHttpClient>();
    }

    [McpServerTool, Description("Gets details for a pipeline run")]
    public async Task<string> GetPipelineRun(int buildId, string? project = null)
    {
        // _project state changes to the last successful GET to the build api
        project ??= this.project ?? "public";

        try
        {
            var build = await this.buildClient!.GetBuildAsync(project, buildId);
            this.project = project;
            return JsonSerializer.Serialize(build);
        }
        catch { }
        try
        {
            project = project == "public" ? "internal" : "public";
            var build = await this.buildClient!.GetBuildAsync(project, buildId);
            this.project = project;
            return JsonSerializer.Serialize(build);
        }
        catch { }

        throw new Exception($"Failed to find build {buildId} in project 'public' or 'internal'");
    }

    [McpServerTool, Description("Gets failures from non-test steps in a pipeline run")]
    public async Task<string> GetPipelineFailures(int buildId)
    {
        var failedNonTests = await GetPipelineFailuresTyped(buildId);
        return JsonSerializer.Serialize(failedNonTests);
    }

    public async Task<List<TimelineRecord>> GetPipelineFailuresTyped(int buildId)
    {
        var timeline = await this.buildClient!.GetBuildTimelineAsync(this.project, buildId);
        var failedNonTests = timeline.Records.Where(r => r.Result == TaskResult.Failed && !IsTestStep(r.Name)).ToList();
        return failedNonTests;
    }


    [McpServerTool, Description(@"
        Analyze and diagnose the failed test results from a pipeline.
        Include relevant data like test name and environment, error type, error messages, functions and error lines.
        Provide suggested next steps.
    ")]
    public async Task<string> GetPipelineFailedTestResults(int buildId)
    {
        var results = new List<ShallowTestCaseResult>();
        var testRuns = await testClient!.GetTestResultsByPipelineAsync(project, buildId);
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

        return JsonSerializer.Serialize(failedRunData);
    }

    [McpServerTool, Description(@"
        Get the failed steps from a pipeline that are not test steps. Show detailed information/logs when available.
        Find other log lines in addition to the final error that may be descriptive of the problem.
        For example, 'Powershell exited with code 1' is not an error message, but the error message may be in the logs above it.
    ")]
    public async Task<string> GetPipelineFailureLog(int buildId, int logId, string? project = null)
    {
        project ??= this.project ?? "public";
        var logContent = await this.buildClient!.GetBuildLogLinesAsync(project, buildId, logId);
        var output = new List<string>();
        foreach (var line in logContent)
        {
            output.Add(line);
        }
        return JsonSerializer.Serialize(string.Join("\n", output));
    }

    public async Task<string> AnalyzePipelineFailureLog(int buildId, int logId, string? project = null)
    {
        project ??= this.project ?? "public";
        var logContent = await this.buildClient!.GetBuildLogLinesAsync(project, buildId, logId);
        var logText = string.Join("\n", logContent);
        var logBytes = System.Text.Encoding.UTF8.GetBytes(logText);
        var session = $"{this.project}-{buildId}-{logId}";
        var filename = $"{session}.txt";

        using var stream = new MemoryStream(logBytes);
        var (response, usage) = await this.aiAgentService!.QueryFileAsync(stream, filename, session, "Why did this pipeline fail?");
        usage.LogCost();

        return response;
    }

    public bool IsTestStep(string stepName)
    {
        return stepName.Contains("test", StringComparison.OrdinalIgnoreCase);
    }
}
