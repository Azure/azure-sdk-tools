// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.TeamFoundation.Build.WebApi;

namespace Azure.Sdk.Tools.Cli.Helpers.Pipeline;

/// <summary>
/// Analyzes already-resolved Azure Pipelines runs: downloads and analyzes failure logs and failed test
/// results.
/// </summary>
public interface IPipelineAnalysisHelper
{
    /// <summary>
    /// Analyzes the given builds, returning one analysis per build plus any warnings that apply to the
    /// overall result (for example test artifacts that could not be parsed).
    /// </summary>
    Task<(List<AzurePipelineAnalysis> AzurePipelineAnalyses, List<string> Warnings)> AnalyzePipelineAsync(
        List<AzurePipelineBuild> builds,
        int? logId = null,
        CancellationToken ct = default);

    /// <summary>Downloads and analyzes the specific failure logs for a build.</summary>
    Task<LogAnalysisResponse> AnalyzePipelineFailureLogsAsync(
        AzurePipelineBuild build,
        List<int> logIds,
        CancellationToken ct);
}

public class PipelineAnalysisHelper(
    IDevOpsService devopsService,
    ILogAnalysisHelper logAnalysisHelper,
    ITestResultParserResolver parserResolver,
    ILogger<PipelineAnalysisHelper> logger
) : IPipelineAnalysisHelper
{
    public async Task<(List<AzurePipelineAnalysis> AzurePipelineAnalyses, List<string> Warnings)> AnalyzePipelineAsync(
        List<AzurePipelineBuild> builds,
        int? logId = null,
        CancellationToken ct = default)
    {
        List<AzurePipelineAnalysis> pipelineAnalyses = [];
        List<string> warnings = [];

        foreach (var build in builds)
        {
            // Each build is analyzed independently so a failure reading one run does not abort the batch.
            var pipelineAnalysis = new AzurePipelineAnalysis
            {
                PipelineBuild = build
            };
            pipelineAnalyses.Add(pipelineAnalysis);

            var recoveredFailedTests = false;
            try
            {
                var (failedTests, skippedArtifacts) = await AnalyzeBuildTestArtifactsAsync(build, ct);
                recoveredFailedTests = failedTests.Count > 0;
                pipelineAnalysis.FailedPipelineTests = recoveredFailedTests ? failedTests : null;

                if (skippedArtifacts.Count > 0)
                {
                    // Surface partial results to the caller (not only the log) so an incomplete test analysis is visible.
                    warnings.Add(
                        $"Build {build.BuildId}: test results may be incomplete: {skippedArtifacts.Count} artifact(s) " +
                        "could not be read or parsed (missing file, unrecognized format, or malformed content): " +
                        $"{string.Join(", ", skippedArtifacts)}.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to analyze failed-test artifacts for build {buildId}", build.BuildId);
                (pipelineAnalysis.Errors ??= []).Add($"Failed to analyze test artifacts: {ex.Message}");
                // Leaving recoveredFailedTests false widens the log search below.
            }

            try
            {
                var logIds = logId.HasValue && logId.Value != 0
                    ? [logId.Value]
                    : await getPipelineFailureLogIds(build, recoveredFailedTests, ct);
                var pipelineFailureLogs = await AnalyzePipelineFailureLogsAsync(build, logIds, ct);
                if (pipelineFailureLogs.HasErrors)
                {
                    pipelineAnalysis.FailedPipelineTasks = pipelineFailureLogs;
                }
                else if (!string.IsNullOrEmpty(pipelineFailureLogs.ResponseError))
                {
                    (pipelineAnalysis.Errors ??= []).Add(pipelineFailureLogs.ResponseError);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to analyze pipeline failure logs for build {buildId}", build.BuildId);
                (pipelineAnalysis.Errors ??= []).Add($"Failed to analyze failure logs: {ex.Message}");
            }
        }

        return (pipelineAnalyses, warnings);
    }

    /// <summary>
    /// Parses the build's failed-test artifacts into one entry per artifact file (file, platform, failing
    /// titles). Artifacts that cannot be read or parsed are returned to the caller rather than reported here.
    /// </summary>
    private async Task<(List<FailedTestArtifact> FailedTests, List<string> SkippedArtifacts)> AnalyzeBuildTestArtifactsAsync(AzurePipelineBuild build, CancellationToken ct)
    {
        var failedTests = new List<FailedTestArtifact>();
        var failedTestArtifacts = await devopsService.GetPipelineLlmArtifacts(build.Project, build.BuildId, ct);
        var skippedArtifacts = new List<string>();

        foreach (var testFiles in failedTestArtifacts)
        {
            foreach (var file in testFiles.Value)
            {
                try
                {
                    var parser = await parserResolver.ResolveAsync(file, ct);
                    var failed = await parser.GetFailedTestCases(file, ct: ct);
                    if (failed.Items.Count == 0)
                    {
                        continue;
                    }

                    failedTests.Add(new FailedTestArtifact
                    {
                        ArtifactFilePath = file,
                        Platform = testFiles.Key,
                        FailedTestTitles = failed.Items.Select(t => t.TestCaseTitle).ToList(),
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipping test result artifact {FilePath}", file);
                    skippedArtifacts.Add(file);
                }
            }
        }

        return (failedTests, skippedArtifacts);
    }

    /// <summary>
    /// Collects the log ids of the build's failed tasks. Test steps are excluded only when the build's test
    /// results were recovered from its artifacts.
    /// </summary>
    private async Task<List<int>> getPipelineFailureLogIds(AzurePipelineBuild build, bool excludeTestSteps, CancellationToken ct = default)
    {
        logger.LogDebug("Getting pipeline task failures for {project} {buildId}", build.Project, build.BuildId);

        var timeline = await devopsService.GetBuildTimelineAsync(build.Project, build.BuildId, ct);
        var failedTasks = timeline.Records
            .Where(r => r.Result == TaskResult.Failed
                && r.RecordType == "Task"
                && !(excludeTestSteps && isTestStep(r.Name)))
            .ToList();
        logger.LogDebug("Found {count} failed tasks", failedTasks.Count);
        return failedTasks.Select(t => t.Log?.Id ?? 0).Where(id => id != 0).Distinct().ToList();
    }

    public async Task<LogAnalysisResponse> AnalyzePipelineFailureLogsAsync(AzurePipelineBuild build, List<int> logIds, CancellationToken ct)
    {
        try
        {
            return await analyzePipelineFailureLogsAsync(build, logIds, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze pipeline {buildId}", build.BuildId);
            return new LogAnalysisResponse()
            {
                ResponseError = $"Failed to analyze pipeline {build.BuildId}: {ex.Message}",
            };
        }
    }

    private async Task<LogAnalysisResponse> analyzePipelineFailureLogsAsync(AzurePipelineBuild build, List<int> logIds, CancellationToken ct)
    {
        List<string> logFilePaths = [];

        try
        {
            foreach (var logId in logIds)
            {
                logger.LogDebug("Downloading pipeline failure log for {project} {buildId} {logId}", build.Project, build.BuildId, logId);
                var logContent = await devopsService.GetBuildLogLinesAsync(build.Project, build.BuildId, logId, ct);
                var logText = string.Join("\n", logContent);

                var tempPath = Path.Combine(Path.GetTempPath(), $"log-analysis-{Guid.NewGuid():N}.txt");
                logger.LogDebug("Writing log id {logId} to temporary file {tempPath}", logId, tempPath);
                await File.WriteAllTextAsync(tempPath, logText, ct);
                logFilePaths.Add(tempPath);
            }

            LogAnalysisResponse response = new()
            {
                PipelineUrl = build.PipelineUrl,
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

    private bool isTestStep(string stepName)
    {
        if (stepName.Contains("deploy test resources", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return stepName.Contains("test", StringComparison.OrdinalIgnoreCase);
    }
}
