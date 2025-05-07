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
public class AzurePipelinesTool(
    Lazy<IAzureService> azureService,
    Lazy<IAIAgentService> aiAgentServiceWrapper,
    ILogger<AzurePipelinesTool> logger) : MCPTool
{
    public string? project;

    private BuildHttpClient? buildClient;
    private TestResultsHttpClient? testClient;
    private readonly Lazy<IAzureService> azureService = azureService;
    private readonly Lazy<IAIAgentService> aiAgentServiceWrapper = aiAgentServiceWrapper;
    private IAIAgentService? aiAgentService;
    private readonly ILogger<AzurePipelinesTool> logger = logger;
    private readonly Boolean initialized = false;

    // Commands
    private readonly string getPipelineRunCommandName = "get-pipeline-run";
    private readonly string analyzePipelineCommandName = "analyze";

    // Options
    private readonly Option<int> buildIdOpt = new(["--build-id", "-b"], "Pipeline/Build ID") { IsRequired = true };
    private readonly Option<string> projectOpt = new(["--project", "-p"], () => "public", "Pipeline project name") { IsRequired = true };
    private readonly Option<int> logIdOpt = new(["--log-id"], "ID of the pipeline task log") { IsRequired = true };

    public override Command GetCommand()
    {
        Console.WriteLine($"BBP IN COMMAND FUNC");
        Command command = new("azp", "Azure Pipelines Tool");

        command.AddCommand(new Command(this.getPipelineRunCommandName, "Get details for a pipeline run") { this.buildIdOpt, this.projectOpt });
        command.AddCommand(new Command(this.analyzePipelineCommandName, "Analyze a pipeline run") { this.buildIdOpt, this.projectOpt, this.logIdOpt });
        command.SetHandler(async ctx =>
        {
            Console.WriteLine($"BBP IN HANDLER FUNC");
            ctx.ExitCode = await HandleCommand(ctx, ctx.GetCancellationToken());
        });

        return command;
    }

    public override async Task<int> HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        Initialize();

        var cmd = ctx.ParseResult.CommandResult.Command.Name;

        if (cmd == this.getPipelineRunCommandName)
        {
            var buildId = ctx.ParseResult.GetValueForOption(this.buildIdOpt);
            var projectId = ctx.ParseResult.GetValueForOption(this.projectOpt);
            var result = await GetPipelineRun(buildId);
            this.logger.LogInformation("{result}", result);
        }
        else if (cmd == this.analyzePipelineCommandName)
        {
            var buildId = ctx.ParseResult.GetValueForOption(this.buildIdOpt);
            var projectId = ctx.ParseResult.GetValueForOption(this.projectOpt);
            var logId = ctx.ParseResult.GetValueForOption(this.logIdOpt);
            var result = await AnalyzePipelineFailureLog(projectId!, buildId, logId);
            this.logger.LogInformation("{result}", result);
        }

        throw new NotImplementedException($"Command {cmd} not implemented");
    }

    private void Initialize()
    {
        if (this.initialized)
        {
            return;
        }
        var tokenScope = new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" };  // Azure DevOps scope
        var token = this.azureService.Value.GetCredential().GetToken(new TokenRequestContext(tokenScope));
        var tokenCredential = new VssOAuthAccessTokenCredential(token.Token);
        var connection = new VssConnection(new Uri($"https://dev.azure.com/azure-sdk"), tokenCredential);
        this.buildClient = connection.GetClient<BuildHttpClient>();
        this.testClient = connection.GetClient<TestResultsHttpClient>();
        this.aiAgentService = this.aiAgentServiceWrapper.Value;
    }

    [McpServerTool, Description("Gets details for a pipeline run")]
    public async Task<string> GetPipelineRun(int buildId)
    {
        // _project state changes to the last successful GET to the build api
        var project = this.project ?? "public";

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
    public async Task<string> GetPipelineFailureLog(int buildId, int logId)
    {
        var logContent = await this.buildClient!.GetBuildLogLinesAsync(project, buildId, logId);
        var output = new List<string>();
        foreach (var line in logContent)
        {
            output.Add(line);
        }
        return JsonSerializer.Serialize(string.Join("\n", output));
    }

    public async Task<string> AnalyzePipelineFailureLog(string project, int buildId, int logId)
    {
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
