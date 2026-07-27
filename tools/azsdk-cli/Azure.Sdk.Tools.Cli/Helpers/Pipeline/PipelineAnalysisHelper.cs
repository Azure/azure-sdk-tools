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
/// results. Resolution of identifiers to builds and commits is the identifier helper's job, and GitHub
/// Actions runs are the workflow helper's; this type only consumes what it is handed and returns its own
/// findings, so the caller decides how they are presented.
/// </summary>
public interface IPipelineAnalysisHelper
{
    /// <summary>
    /// Analyzes the given builds, returning one analysis per build plus any warnings that apply to the
    /// overall result (for example test artifacts that could not be parsed).
    /// </summary>
    Task<(List<BuildAnalysis> BuildAnalyses, List<string> Warnings)> AnalyzePipelineAsync(
        List<ResolvedBuild> builds,
        int? logId = null,
        CancellationToken ct = default);

    /// <summary>Downloads and analyzes the specific failure logs for a build.</summary>
    Task<LogAnalysisResponse> AnalyzePipelineFailureLogsAsync(
        ResolvedBuild build,
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
    public async Task<(List<BuildAnalysis> BuildAnalyses, List<string> Warnings)> AnalyzePipelineAsync(
        List<ResolvedBuild> builds,
        int? logId = null,
        CancellationToken ct = default)
    {
        List<BuildAnalysis> buildAnalyses = [];
        List<string> warnings = [];

        foreach (var build in builds)
        {
            // Each build is analyzed independently so a failure reading one run does not abort the batch.
            var buildAnalysis = new BuildAnalysis
            {
                Build = build
            };
            buildAnalyses.Add(buildAnalysis);

            // Step 1: Analyze failed-test artifacts for the whole build.
            var recoveredFailedTests = false;
            try
            {
                var (failedTests, skippedArtifacts) = await AnalyzeBuildTestArtifactsAsync(build, ct);
                buildAnalysis.FailedBuildTests.AddRange(failedTests);
                recoveredFailedTests = failedTests.Count > 0;

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
                (buildAnalysis.Errors ??= []).Add($"Failed to analyze test artifacts: {ex.Message}");
                // Leaving recoveredFailedTests false widens the log search below.
            }

            // Step 2: Analyze failure logs (either the caller-specified log or every failed task's log).
            try
            {
                var logIds = logId.HasValue && logId.Value != 0
                    ? [logId.Value]
                    : await getPipelineFailureLogIds(build, recoveredFailedTests, ct);
                var pipelineFailureLogs = await AnalyzePipelineFailureLogsAsync(build, logIds, ct);
                if (pipelineFailureLogs.HasErrors)
                {
                    buildAnalysis.FailedBuildTasks = pipelineFailureLogs;
                }
                else if (!string.IsNullOrEmpty(pipelineFailureLogs.ResponseError))
                {
                    (buildAnalysis.Errors ??= []).Add(pipelineFailureLogs.ResponseError);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to analyze pipeline failure logs for build {buildId}", build.BuildId);
                (buildAnalysis.Errors ??= []).Add($"Failed to analyze failure logs: {ex.Message}");
            }
        }

        return (buildAnalyses, warnings);
    }

    /// <summary>
    /// Parses the failed-test artifacts published by a build. Artifacts that cannot be read or parsed are
    /// returned to the caller rather than reported here, so the decision of how to surface a partial result
    /// stays with the code that owns the response.
    /// </summary>
    private async Task<(List<FailedTestRunResponse> FailedTests, List<string> SkippedArtifacts)> AnalyzeBuildTestArtifactsAsync(ResolvedBuild build, CancellationToken ct)
    {
        var failedTests = new FailedTestRunListResponse();
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
                    failedTests.Items.AddRange(failed.Items);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skipping test result artifact {FilePath}", file);
                    skippedArtifacts.Add(file);
                }
            }
        }

        return (failedTests.Items, skippedArtifacts);
    }

    /// <summary>
    /// Collects the log ids of the build's failed tasks. Test steps are excluded only when the build's test
    /// results were recovered from its artifacts, because those describe a test failure far better than
    /// scraping the step's log does. When no test results were recovered the step's log is kept: a test step
    /// also restores and compiles, and when it fails at those stages its log is the only record of why.
    /// </summary>
    private async Task<List<int>> getPipelineFailureLogIds(ResolvedBuild build, bool excludeTestSteps, CancellationToken ct = default)
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

    public async Task<LogAnalysisResponse> AnalyzePipelineFailureLogsAsync(ResolvedBuild build, List<int> logIds, CancellationToken ct)
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

    private async Task<LogAnalysisResponse> analyzePipelineFailureLogsAsync(ResolvedBuild resolvedBuild, List<int> logIds, CancellationToken ct)
    {
        List<string> logFilePaths = [];

        try
        {
            foreach (var logId in logIds)
            {
                logger.LogDebug("Downloading pipeline failure log for {project} {buildId} {logId}", resolvedBuild.Project, resolvedBuild.BuildId, logId);
                var logContent = await devopsService.GetBuildLogLinesAsync(resolvedBuild.Project, resolvedBuild.BuildId, logId, ct);
                var logText = string.Join("\n", logContent);

                var tempPath = Path.Combine(Path.GetTempPath(), $"log-analysis-{Guid.NewGuid():N}.txt");
                logger.LogDebug("Writing log id {logId} to temporary file {tempPath}", logId, tempPath);
                await File.WriteAllTextAsync(tempPath, logText, ct);
                logFilePaths.Add(tempPath);
            }

            LogAnalysisResponse response = new()
            {
                PipelineUrl = resolvedBuild.PipelineUrl,
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
