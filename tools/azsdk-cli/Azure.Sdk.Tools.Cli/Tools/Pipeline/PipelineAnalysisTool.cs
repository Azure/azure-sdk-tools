// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using Azure.Core;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.CopilotAgents;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.Pipeline;

[McpServerToolType, Description("Fetches data from an Azure Pipelines run.")]
public class PipelineAnalysisTool(
    IPipelineIdentifierHelper pipelineHelper,
    IHttpClientFactory httpClientFactory,
    IAzureService azureService,
    IDevOpsService devopsService,
    ILogAnalysisHelper logAnalysisHelper,
    ITestHelper testHelper,
    ICopilotAgentRunner copilotAgentRunner,
    ILogger<PipelineAnalysisTool> logger
) : MCPTool
{
    private readonly Option<int> logIdOpt = new("--log-id")
    {
        Description = "ID of the pipeline task log",
        Required = false,
    };

    private readonly Option<string> projectOpt = new("--project", "-p")
    {
        Description = "Pipeline project name",
        Required = false,
    };

    private readonly Option<bool> copilotOpt = new("--copilot")
    {
        Description = "Use Copilot agent to analyze pipeline failures",
        Required = false,
        DefaultValueFactory = _ => false,
    };

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.AzurePipelines];

    private const string AnalyzePipelineToolName = "azsdk_analyze_pipeline";

    protected override Command GetCommand() =>
        new McpCommand("analyze", "Analyze a pipeline run", AnalyzePipelineToolName)
        {
            SharedOptions.PipelineLocator, projectOpt, logIdOpt, copilotOpt,
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var pipelineIdentifier = parseResult.GetValue(SharedOptions.PipelineLocator);
        var project = parseResult.GetValue(projectOpt);
        var logId = parseResult.GetValue(logIdOpt);
        var useCopilot = parseResult.GetValue(copilotOpt);

        logger.LogInformation("Analyzing pipeline {pipelineIdentifier}...", pipelineIdentifier);
        var result = await AnalyzePipeline(pipelineIdentifier, project, logId != 0 ? logId : null, ct);

        if (!useCopilot)
        {
            return result;
        }

        return await AnalyzeWithCopilot(result, pipelineIdentifier, ct);
    }

    private async Task<CommandResponse> AnalyzeWithCopilot(AnalyzePipelineResponse pipelineResult, string pipelineIdentifier, CancellationToken ct)
    {
        try
        {
            // Serialize as JSON to ensure Copilot gets the full context (FailedTasks, FailedTests)
            // even when ResponseErrors is set, since ToString() suppresses Format() output on errors.
            var pipelineData = JsonSerializer.Serialize(pipelineResult, new JsonSerializerOptions { WriteIndented = true });

            var tempPath = Path.Combine(Path.GetTempPath(), $"pipeline-analysis-{Guid.NewGuid():N}.md");
            await File.WriteAllTextAsync(tempPath, pipelineData, ct);
            logger.LogInformation("Pipeline analysis data written to {tempPath}", tempPath);
            logger.LogInformation("Run `copilot -i 'Fix the pipeline failures detailed in {tempPath}'` to attempt a fix", tempPath);

            var instructions = $"""
                You are a pipeline failure analyst. You have been given the output of a CI/CD pipeline analysis.
                Your job is to examine the failed tasks and failed tests, identify root causes, and provide
                a clear, actionable summary for a developer.

                Respond in markdown format. Structure your response as:
                1. **Root Cause Analysis** - What likely caused each failure
                2. **Summary** - A brief overview of what failed
                3. **Recommended Actions** - Concrete steps the developer should take to fix the issues

                If there are no failures, state that the pipeline appears healthy.

                Here is the pipeline analysis data:

                {pipelineData}
                """;

            var agent = new CopilotAgent<string>
            {
                Instructions = instructions,
                MaxIterations = 3,
            };

            var analysis = await copilotAgentRunner.RunAsync(agent, ct);

            return new DefaultCommandResponse { Message = analysis };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run Copilot analysis for pipeline {pipelineIdentifier}", pipelineIdentifier);
            return new DefaultCommandResponse
            {
                ResponseError = $"Failed to run Copilot analysis: {ex.Message}"
            };
        }
    }

    private bool initialized = false;
    private BuildHttpClient buildClientValue;
    private BuildHttpClient buildClient
    {
        get
        {
            Initialize();
            return buildClientValue;
        }
    }
    private TestResultsHttpClient testClientValue;
    private TestResultsHttpClient testClient
    {
        get
        {
            Initialize();
            return testClientValue;
        }
    }

    private void Initialize(bool auth = true, CancellationToken ct = default)
    {
        if (initialized)
        {
            return;
        }

        if (auth)
        {
            var tokenScope = new[] { Constants.AZURE_DEVOPS_TOKEN_SCOPE };
            var token = azureService.GetCredential(Constants.MICROSOFT_CORP_TENANT).GetToken(new TokenRequestContext(tokenScope), ct);
            var tokenCredential = new VssOAuthAccessTokenCredential(token.Token);
            var connection = new VssConnection(new Uri(Constants.AZURE_SDK_DEVOPS_BASE_URL), tokenCredential);
            buildClientValue = connection.GetClient<BuildHttpClient>();
            testClientValue = connection.GetClient<TestResultsHttpClient>();
        }
        else
        {
            var connection = new VssConnection(new Uri(Constants.AZURE_SDK_DEVOPS_BASE_URL), null);
            buildClientValue = connection.GetClient<BuildHttpClient>();
            testClientValue = connection.GetClient<TestResultsHttpClient>();
        }

        initialized = true;
    }

    private async Task<List<int>> getPipelineFailureLogIds(string project, int buildId, CancellationToken ct = default)
    {
        logger.LogDebug("Getting pipeline task failures for {project} {buildId}", project, buildId);

        if (project != Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT)
        {
            var timeline = await buildClient.GetBuildTimelineAsync(project, buildId, cancellationToken: ct);
            var _failedTasks = timeline.Records.Where(
                                    r => r.Result == TaskResult.Failed
                                    && r.RecordType == "Task"
                                    && !isTestStep(r.Name))
                                .ToList();
            logger.LogDebug("Found {count} failed tasks", _failedTasks.Count);
            return _failedTasks.Select(t => t.Log?.Id ?? 0).Where(id => id != 0).Distinct().ToList();
        }

        var timelineUrl = $"{Constants.AZURE_SDK_DEVOPS_BASE_URL}/{project}/_apis/build/builds/{buildId}/timeline?api-version=7.1";
        logger.LogDebug("Getting timeline records from {url}", timelineUrl);
        var response = await httpClientFactory.CreateClient().GetAsync(timelineUrl, ct);
        // Devops will return a sign-in html page if the user is not authorized
        if (response.StatusCode == System.Net.HttpStatusCode.NonAuthoritativeInformation)
        {
            throw new Exception($"Not authorized to get timeline records from {timelineUrl}");
        }
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrEmpty(json))
        {
            throw new Exception($"No timeline records found for build {buildId} in project {project}");
        }

        using var doc = JsonDocument.Parse(json);
        var failedTasks = doc.RootElement.GetProperty("records")
            .EnumerateArray()
            .Where(r =>
                r.GetProperty("result").GetString() == "failed" &&
                r.GetProperty("type").GetString() == "Task" &&
                !isTestStep(r.GetProperty("name").GetString())).ToList();

        List<int> logIds = [];
        foreach (var task in failedTasks)
        {
            if (task.TryGetProperty("log", out var logProp) && logProp.TryGetProperty("id", out var idProp))
            {
                var id = idProp.GetInt32();
                if (id != 0)
                {
                    logIds.Add(id);
                }
            }
        }

        logger.LogDebug("Found {count} failed tasks", failedTasks.Count);
        return logIds;
    }

    private async Task<FailedTestRunListResponse> getPipelineFailedTestResults(string project, int buildId, CancellationToken ct = default)
    {
        try
        {
            logger.LogDebug("Getting pipeline failed test results for {project} {buildId}", project, buildId);
            var results = new List<ShallowTestCaseResult>();

            var testRuns = await testClient.GetTestResultsByPipelineAsync(project, buildId, cancellationToken: ct);
            results.AddRange(testRuns);
            while (testRuns.ContinuationToken != null)
            {
                var nextResults = await testClient.GetTestResultsByPipelineAsync(project, buildId, continuationToken: testRuns.ContinuationToken, cancellationToken: ct);
                results.AddRange(nextResults);
                testRuns.ContinuationToken = nextResults.ContinuationToken;
            }

            var failedRuns = results.Where(
                r => r.Outcome == TestOutcome.Failed.ToString()
                || r.Outcome == TestOutcome.Aborted.ToString())
            .Select(r => r.RunId)
            .Distinct()
            .ToList();

            logger.LogDebug("Getting test results for {count} failed test runs", failedRuns.Count);

            var failedRunData = new FailedTestRunListResponse();

            foreach (var runId in failedRuns)
            {
                var testCases = await testClient.GetTestResultsAsync(
                                    project,
                                    runId,
                                    outcomes: [TestOutcome.Failed, TestOutcome.Aborted],
                                    cancellationToken: ct);

                foreach (var tc in testCases)
                {
                    failedRunData.Items.Add(new FailedTestRunResponse
                    {
                        RunId = runId,
                        TestCaseTitle = tc.TestCaseTitle,
                        ErrorMessage = tc.ErrorMessage,
                        StackTrace = tc.StackTrace,
                        Outcome = tc.Outcome,
                        Uri = tc.Url
                    });
                }
            }

            return failedRunData;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get pipeline failed test results {buildId}", buildId);
            return new() { ResponseError = $"Failed to get pipeline failed test results {buildId}: {ex.Message}" };
        }
    }

    private async Task<string> getBuildLogLinesUnauthenticated(string project, int buildId, int logId, CancellationToken ct = default)
    {
        var logUrl = $"{Constants.AZURE_SDK_DEVOPS_BASE_URL}/{project}/_apis/build/builds/{buildId}/logs/{logId}?api-version=7.1";
        logger.LogDebug("Fetching log file from {url}", logUrl);
        var response = await httpClientFactory.CreateClient().GetAsync(logUrl, ct);
        // Devops will return a sign-in html page if the user is not authorized
        if (response.StatusCode == System.Net.HttpStatusCode.NonAuthoritativeInformation)
        {
            throw new Exception($"Not authorized to get log file from {logUrl}");
        }
        response.EnsureSuccessStatusCode();
        var logContent = await response.Content.ReadAsStringAsync(ct);
        return logContent;
    }

    public async Task<LogAnalysisResponse> AnalyzePipelineFailureLogs(string? project, int buildId, List<int> logIds, CancellationToken ct)
    {
        try
        {
            return await analyzePipelineFailureLogs(project, buildId, logIds, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze pipeline {buildId}", buildId);
            return new LogAnalysisResponse()
            {
                ResponseError = $"Failed to analyze pipeline {buildId}: {ex.Message}",
            };
        }
    }

    private async Task<LogAnalysisResponse> analyzePipelineFailureLogs(string? project, int buildId, List<int> logIds, CancellationToken ct)
    {
        project ??= Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT;
        List<string> logFilePaths = [];

        try
        {
            foreach (var logId in logIds)
            {
                string logText;
                logger.LogDebug("Downloading pipeline failure log for {project} {buildId} {logId}", project, buildId, logId);

                if (project == Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT)
                {
                    logText = await getBuildLogLinesUnauthenticated(project, buildId, logId, ct);
                }
                else
                {
                    var logContent = await buildClient.GetBuildLogLinesAsync(project, buildId, logId, cancellationToken: ct);
                    logText = string.Join("\n", logContent);
                }

                var tempPath = Path.Combine(Path.GetTempPath(), $"log-analysis-{Guid.NewGuid():N}.txt");
                logger.LogDebug("Writing log id {logId} to temporary file {tempPath}", logId, tempPath);
                await File.WriteAllTextAsync(tempPath, logText, ct);
                logFilePaths.Add(tempPath);
            }

            LogAnalysisResponse response = new()
            {
                PipelineUrl = pipelineHelper.GetPipelineUrl(project, buildId),
                Errors = []
            };

            foreach (var log in logFilePaths)
            {
                var localLogResult = await logAnalysisHelper.AnalyzeLogContent(log, null, null, null, ct);
                response.Errors.AddRange(localLogResult);
            }

            return response;
        }
        finally
        {
            foreach (var log in logFilePaths)
            {
                try { File.Delete(log); }
                catch (Exception ex) { logger.LogDebug(ex, "Failed to clean up temp file {log}", log); }
            }
        }
    }

    [McpServerTool(Name = AnalyzePipelineToolName), Description("Analyze what happened in an Azure pipeline build. Investigates pipeline runs, identifies failures, and explains build issues. Accepts an Azure Pipeline link, Build ID, GitHub Pull Request link, or Pull Request number.")]
    public async Task<AnalyzePipelineResponse> AnalyzePipeline(
        [Description("Azure Pipeline link, Build ID, GitHub Pull Request link, or PR number")] string pipelineIdentifier,
        [Description("Pipeline project name (optional)")] string? project = null,
        [Description("Specific log ID to analyze (optional)")] int? logId = null,
        CancellationToken ct = default)
    {
        try
        {
            var builds = await pipelineHelper.ResolveBuildsAsync(pipelineIdentifier, project, ct);

            if (builds.Count == 0)
            {
                return new AnalyzePipelineResponse
                {
                    ResponseError = $"No failed Azure Pipeline builds found for {pipelineIdentifier}"
                };
            }

            var aggregatedResponse = new AnalyzePipelineResponse();

            foreach (var build in builds)
            {
                var buildProject = build.Project;

                if (logId.HasValue && logId.Value != 0)
                {
                    var logResult = await AnalyzePipelineFailureLogs(buildProject, build.BuildId, [logId.Value], ct);
                    if (logResult.HasErrors)
                    {
                        aggregatedResponse.FailedTasks.Add(logResult);
                    }
                }
                else
                {
                    var result = await AnalyzePipelineInternal(buildProject, build.BuildId, ct);
                    if (result.ResponseError != null)
                    {
                        aggregatedResponse.ResponseErrors ??= [];
                        aggregatedResponse.ResponseErrors.Add($"Build {build.BuildId}: {result.ResponseError}");
                        continue;
                    }

                    aggregatedResponse.FailedTasks.AddRange(result.FailedTasks);
                    foreach (var (key, value) in result.FailedTests)
                    {
                        if (aggregatedResponse.FailedTests.TryGetValue(key, out var existing))
                        {
                            existing.AddRange(value);
                        }
                        else
                        {
                            aggregatedResponse.FailedTests[key] = value;
                        }
                    }
                }
            }

            return aggregatedResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze pipeline {pipelineIdentifier}", pipelineIdentifier);
            return new AnalyzePipelineResponse()
            {
                ResponseError = $"Failed to analyze pipeline {pipelineIdentifier}: {ex.Message}",
            };
        }
    }

    private async Task<AnalyzePipelineResponse> AnalyzePipelineInternal(string? project, int buildId, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(project))
            {
                project = await pipelineHelper.GetPipelineProjectAsync(buildId, null, ct);
            }

            var failureLogIds = await getPipelineFailureLogIds(project, buildId, ct);
            var analysis = await analyzePipelineFailureLogs(project, buildId, failureLogIds, ct);

            var failedTests = new FailedTestRunListResponse();
            var failedTestArtifacts = await devopsService.GetPipelineLlmArtifacts(project, buildId, ct);

            foreach (var testFiles in failedTestArtifacts)
            {
                foreach (var file in testFiles.Value)
                {
                    var failed = await testHelper.GetFailedTestCases(file, ct: ct);
                    failedTests.Items.AddRange(failed.Items);
                }
            }

            var failedTestsByUri = failedTests.Items
                .GroupBy(ft => ft.Uri)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(ft => ft.TestCaseTitle).ToList()
                );

            return new AnalyzePipelineResponse()
            {
                FailedTasks = analysis.HasErrors ? [analysis] : [],
                FailedTests = failedTestsByUri
            };
        }
        catch (Exception ex) when (IsAuthException(ex) && project != Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT)
        {
            logger.LogError(ex, "Authorization failure analyzing pipeline {buildId} in project {project}", buildId, project);
            return new AnalyzePipelineResponse()
            {
                ResponseError = $"Not authorized to access build {buildId} in project '{project}'. " +
                    "This is an internal build requiring authentication. " +
                    "Ensure you are signed in with `az login` using an account with access to the Azure SDK DevOps organization.",
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze pipeline {buildId}", buildId);
            return new AnalyzePipelineResponse()
            {
                ResponseError = $"Failed to analyze pipeline {buildId}: {ex.Message}",
            };
        }
    }

    private bool isTestStep(string stepName)
    {
        if (stepName.Contains("deploy test resources", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return stepName.Contains("test", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAuthException(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("401")
            || message.Contains("403")
            || message.Contains("NonAuthoritativeInformation")
            || message.Contains("Not authorized")
            || message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase);
    }
}
