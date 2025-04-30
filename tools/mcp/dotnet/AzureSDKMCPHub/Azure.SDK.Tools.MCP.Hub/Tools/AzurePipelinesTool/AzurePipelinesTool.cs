using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using Azure.Core;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Azure.SDK.Tools.MCP.Hub.Services.Azure;
using Microsoft.VisualStudio.Services.OAuth;
using Azure.SDK.Tools.MCP.Contract;

namespace Azure.SDK.Tools.MCP.Hub.Tools.AzurePipelinesTool;

[McpServerToolType, Description("Fetches data from Azure Pipelines")]
public class AzurePipelinesTool : MCPHubTool
{
    private string? project;

    private readonly BuildHttpClient buildClient;
    private readonly TestResultsHttpClient testClient;

    public AzurePipelinesTool(IAzureService azureService)
    {
        var tokenScope = new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" };  // Azure DevOps scope
        var token = azureService.GetCredential().GetToken(new TokenRequestContext(tokenScope));
        var tokenCredential = new VssOAuthAccessTokenCredential(token.Token);
        var connection = new VssConnection(new Uri($"https://dev.azure.com/azure-sdk"), tokenCredential);
        this.buildClient = connection.GetClient<BuildHttpClient>();
        this.testClient = connection.GetClient<TestResultsHttpClient>();
    }

    [McpServerTool, Description("Gets details for a pipeline run")]
    public async Task<string> GetPipelineRun(int buildId)
    {
        // _project state changes to the last successful GET to the build api
        var project = this.project ?? "public";
        Console.WriteLine("BEBRODER IN PIPELINE RUN");

        try
        {
            var build = await this.buildClient.GetBuildAsync(project, buildId);
            this.project = project;
            return JsonSerializer.Serialize(build);
        }
        catch { }
        try
        {
            project = project == "public" ? "internal" : "public";
            var build = await this.buildClient.GetBuildAsync(project, buildId);
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
        var timeline = await this.buildClient.GetBuildTimelineAsync(this.project, buildId);
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

        return JsonSerializer.Serialize(failedRunData);
    }

    [McpServerTool, Description(@"
        Get the failed steps from a pipeline that are not test steps. Show detailed information/logs when available.
        Find other log lines in addition to the final error that may be descriptive of the problem.
        For example, 'Powershell exited with code 1' is not an error message, but the error message may be in the logs above it.
    ")]
    public async Task<string> GetPipelineFailureLog(int buildId, int logId)
    {
        var logContent = await buildClient.GetBuildLogLinesAsync(project, buildId, logId);
        var output = new List<string>();
        foreach (var line in logContent)
        {
            output.Add(line);
        }
        return JsonSerializer.Serialize(string.Join("\n", output));
    }

    public bool IsTestStep(string stepName)
    {
        return stepName.Contains("test", StringComparison.OrdinalIgnoreCase);
    }
}