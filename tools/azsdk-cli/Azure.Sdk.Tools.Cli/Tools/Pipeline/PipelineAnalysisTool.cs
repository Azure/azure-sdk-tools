// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Text.Json;
using System.Web;
using Azure.Core;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using ModelContextProtocol.Server;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tools.Pipeline;

[McpServerToolType, Description("Fetches data from an Azure Pipelines run.")]
public class PipelineAnalysisTool(
    IAzureService azureService,
    IDevOpsService devopsService,
    IAzureAgentServiceFactory azureAgentServiceFactory,
    ILogAnalysisHelper logAnalysisHelper,
    ITestHelper testHelper,
    ILogger<PipelineAnalysisTool> logger,
    TokenUsageHelper tokenUsageHelper
) : MCPTool
{
    // Options
    private readonly Argument<string> pipelineArg = new("Pipeline link or Build ID");
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

    private readonly Option<bool> analyzeWithAgentOpt = new("--agent", "-a")
    {
        Description = "Analyze logs with RAG via upstream ai agent",
        Required = false,
        DefaultValueFactory = _ => false,
    };

    private readonly Option<string> projectEndpointOpt = new("--ai-endpoint", "-e")
    {
        Description = "The ai foundry project endpoint for the Azure AI Agent service",
        Required = false,
    };

    private readonly Option<string> aiModelOpt = new("--ai-model")
    {
        Description = "The model to use for the Azure AI Agent",
        Required = false,
    };

    private readonly Option<string> queryOpt = new("--query")
    {
        Description = "Log analysis query for agent mode",
        Required = false,
        DefaultValueFactory = _ => "Why did this pipeline fail?",
    };

    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.AzurePipelines];

    protected override Command GetCommand() =>
        new("analyze", "Analyze a pipeline run")
        {
            pipelineArg, projectOpt, logIdOpt, analyzeWithAgentOpt, projectEndpointOpt, aiModelOpt, queryOpt,
        };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var pipelineIdentifier = parseResult.GetValue(pipelineArg);
        var project = parseResult.GetValue(projectOpt);

        var logId = parseResult.GetValue(logIdOpt);
        var analyzeWithAgent = parseResult.GetValue(analyzeWithAgentOpt);
        var projectEndpoint = parseResult.GetValue(projectEndpointOpt);
        var aiModel = parseResult.GetValue(aiModelOpt);
        var query = parseResult.GetValue(queryOpt);

        var (buildId, projectFromLink) = getBuildIdFromPipelineIdentifier(pipelineIdentifier);
        logger.LogInformation("Analyzing pipeline {pipelineIdentifier}...", pipelineIdentifier);
        azureAgentService = azureAgentServiceFactory.Create(projectEndpoint, aiModel);

        if (logId != 0)
        {
            var result = await AnalyzePipelineFailureLogs(project ?? projectFromLink, buildId, query, [logId], analyzeWithAgent, ct);
            tokenUsageHelper.LogUsage();
            return result;
        }
        else
        {
            var result = await AnalyzePipeline(project ?? projectFromLink, buildId, query, analyzeWithAgent, ct);
            tokenUsageHelper.LogUsage();
            return result;
        }
    }

    private IAzureAgentService azureAgentService;
    private bool initialized = false;

    private readonly HttpClient httpClient = new();
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

    private static (int, string?) getBuildIdFromPipelineIdentifier(string pipelineIdentifier)
    {
        // pipelineIdentifier could be a pipeline link like
        // https://dev.azure.com/azure-sdk/internal/_build/results?buildId=5094469&view=results (buildId 5094469, project internal)
        // or just an id like 5094469 (project will be auto-discovered)
        if (int.TryParse(pipelineIdentifier, out int buildId))
        {
            return (buildId, null);
        }

        string? project = null;
        if (!Uri.TryCreate(pipelineIdentifier, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid pipeline identifier: {pipelineIdentifier}. Expected a valid absolute URI or an integer.");
        }

        // Extract devops project from URI segments
        var segments = uri.Segments.Select(s => s.Trim('/')).ToList();
        if (segments.Count >= 3)
        {
            project = segments[2];
        }

        var query = uri.Query;
        var queryParams = HttpUtility.ParseQueryString(query);
        if (int.TryParse(queryParams.Get("buildId"), out buildId))
        {
            return (buildId, project);
        }

        throw new ArgumentException($"Could not extract buildId from pipeline identifier: {pipelineIdentifier}");
    }

    private void Initialize(bool auth = true)
    {
        if (initialized)
        {
            return;
        }

        if (auth)
        {
            var tokenScope = new[] { Constants.AZURE_DEVOPS_TOKEN_SCOPE };
            var token = azureService.GetCredential(Constants.MICROSOFT_CORP_TENANT).GetToken(new TokenRequestContext(tokenScope), CancellationToken.None);
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

    private async Task<string> GetPipelineProject(int buildId, string? project = null)
    {
        if (project == Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT || string.IsNullOrEmpty(project))
        {
            var pipelineUrl = $"{Constants.AZURE_SDK_DEVOPS_BASE_URL}/{Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT}/_apis/build/builds/{buildId}?api-version=7.1";
            logger.LogDebug("Getting pipeline details from {url} via http", pipelineUrl);
            var response = await httpClient.GetAsync(pipelineUrl);
            // If project is not specified, try both public and internal projects
            if (string.IsNullOrEmpty(project) && !response.IsSuccessStatusCode)
            {
                return await GetPipelineProject(buildId, Constants.AZURE_SDK_DEVOPS_INTERNAL_PROJECT);
            }
            // Devops will return a sign-in html page if the user is not authorized
            if (response.StatusCode == System.Net.HttpStatusCode.NonAuthoritativeInformation)
            {
                throw new Exception($"Not authorized to get pipeline details from {pipelineUrl}");
            }
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var projectName = doc.RootElement.GetProperty("project").GetProperty("name").GetString();
            if (string.IsNullOrEmpty(projectName))
            {
                throw new Exception($"Failed to parse project name from build details for build {buildId}");
            }
            return projectName;
        }

        var _pipelineUrl = $"{Constants.AZURE_SDK_DEVOPS_BASE_URL}/{project}/_apis/build/builds/{buildId}?api-version=7.1";
        logger.LogDebug("Getting pipeline details from {url} via sdk", _pipelineUrl);
        var build = await buildClient.GetBuildAsync(project, buildId);
        return build.Project.Name;
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
        var response = await httpClient.GetAsync(timelineUrl, ct);
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
        var response = await httpClient.GetAsync(logUrl, ct);
        // Devops will return a sign-in html page if the user is not authorized
        if (response.StatusCode == System.Net.HttpStatusCode.NonAuthoritativeInformation)
        {
            throw new Exception($"Not authorized to get log file from {logUrl}");
        }
        response.EnsureSuccessStatusCode();
        var logContent = await response.Content.ReadAsStringAsync(ct);
        return logContent;
    }

    public async Task<LogAnalysisResponse> AnalyzePipelineFailureLogs(string? project, int buildId, string query, List<int> logIds, bool analyzeWithAgent, CancellationToken ct)
    {
        try
        {
            return await analyzePipelineFailureLogs(project, buildId, query, logIds, analyzeWithAgent, ct);
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

    private async Task<LogAnalysisResponse> analyzePipelineFailureLogs(string? project, int buildId, string query, List<int> logIds, bool analyzeWithAgent, CancellationToken ct)
    {
        project ??= Constants.AZURE_SDK_DEVOPS_PUBLIC_PROJECT;
        var session = $"{project}-{buildId}";
        List<string> logs = [];

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

            var tempPath = Path.GetTempFileName() + ".txt";
            logger.LogDebug("Writing log id {logId} to temporary file {tempPath}", logId, tempPath);
            await File.WriteAllTextAsync(tempPath, logText, ct);
            var filename = $"{session}-{logId}.txt";
            logs.Add(tempPath);
        }

        if (!analyzeWithAgent)
        {
            LogAnalysisResponse response = new() { Errors = [] };
            foreach (var log in logs)
            {
                var localLogResult = await logAnalysisHelper.AnalyzeLogContent(log, null, null, null);
                response.Errors.AddRange(localLogResult);
            }
            return response;
        }

        var result = await azureAgentService.QueryFiles(logs, session, query, ct);
        // Sometimes chat gpt likes to wrap the json in markdown
        if (result.StartsWith("```json"))
        {
            result = result[7..].Trim();
        }
        if (result.EndsWith("```"))
        {
            result = result[..^3].Trim();
        }

        foreach (var log in logs)
        {
            File.Delete(log);
        }

        try
        {
            return JsonSerializer.Deserialize<LogAnalysisResponse>(result);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize log analysis response. Raw response: {Response}", result);
            throw;
        }
    }

    [McpServerTool(Name = "azsdk_analyze_pipeline"), Description("Analyze azure pipeline for failures. Set analyzeWithAgent to false unless requested otherwise by the user")]
    public async Task<AnalyzePipelineResponse> AnalyzePipeline(int buildId, string query, bool analyzeWithAgent, CancellationToken ct)
    {
        try
        {
            return await AnalyzePipeline(null, buildId, query, analyzeWithAgent, ct);
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

    public async Task<AnalyzePipelineResponse> AnalyzePipeline(string? project, int buildId, string query, bool analyzeWithAgent, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(project))
            {
                project = await GetPipelineProject(buildId, project);
            }

            var failureLogIds = await getPipelineFailureLogIds(project, buildId, ct);
            var analysis = await analyzePipelineFailureLogs(project, buildId, query, failureLogIds, analyzeWithAgent, ct);

            var failedTests = new FailedTestRunListResponse();
            var failedTestArtifacts = await devopsService.GetPipelineLlmArtifacts(project, buildId);

            foreach (var testFiles in failedTestArtifacts)
            {
                foreach (var file in testFiles.Value)
                {
                    var failed = await testHelper.GetFailedTestCases(file);
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
                FailedTasks = [analysis],
                FailedTests = failedTestsByUri
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
}
