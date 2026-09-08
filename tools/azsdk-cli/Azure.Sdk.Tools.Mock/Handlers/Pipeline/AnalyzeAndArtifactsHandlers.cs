// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Mock.Handlers.Pipeline;

/// <summary>
/// Mock handler for azsdk_analyze_pipeline. Build 6455504 returns the canonical Storage
/// QueueClientOptions parser failure used by the analysis and fixer quality evals; other builds
/// return the generic WidgetClientLiveTests failure used by tool-routing scenarios.
/// </summary>
public class AnalyzePipelineHandler : IMockToolHandler
{
    public string ToolName => "azsdk_analyze_pipeline";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var buildId = MockPipelineIdentifier.GetBuildId(arguments) ?? "90001";
        var buildIdValue = int.TryParse(buildId, out var parsed) ? parsed : 90001;
        var pipelineUrl = $"https://dev.azure.com/azure-sdk/internal/_build/results?buildId={buildId}";
        var isVersionParserFixture = buildId == "6455504";
        var failedTestTitle = isVersionParserFixture
            ? "Azure.Storage.Queues.Tests.QueueClientOptionsTests.TryGetServiceVersion_ParsesAllServiceVersions"
            : "WidgetClientLiveTests.GetWidget";
        var errorMessage = isVersionParserFixture
            ? "QueueClientOptions.TryGetServiceVersion is missing mappings for service versions 2026-10-06 (V2026_10_06) and 2026-12-06 (V2026_12_06)"
            : "Test WidgetClientLiveTests.GetWidget failed: expected 200 got 404";
        return new AnalyzePipelineResponse
        {
            AzurePipelineAnalyses = new List<AzurePipelineAnalysis>
            {
                new AzurePipelineAnalysis
                {
                    PipelineBuild = new AzurePipelineBuild(buildIdValue, "internal", pipelineUrl, "completed", "failed"),
                    FailedPipelineTests = new List<FailedTestArtifact>
                    {
                        new FailedTestArtifact
                        {
                            ArtifactFilePath = $"/tmp/{buildId}/Ubuntu2404_NET80_PackageRef_Debug/test-results.trx",
                            Platform = "Ubuntu2404_NET80_PackageRef_Debug",
                            FailedTestTitles =
                            [
                                failedTestTitle
                            ]
                        }
                    },
                    FailedPipelineTasks = new LogAnalysisResponse
                    {
                        PipelineUrl = pipelineUrl,
                        Errors =
                        [
                            new LogEntry
                            {
                                File = "logs/test.log",
                                Line = 128,
                                Message = errorMessage
                            }
                        ]
                    }
                }
            }
        };
    }
}

/// <summary>Mock handler for azsdk_get_pipeline_llm_artifacts.</summary>
public class GetPipelineLlmArtifactsHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_pipeline_llm_artifacts";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var buildId = MockPipelineIdentifier.GetBuildId(arguments) ?? "90001";
        return new ObjectCommandResponse
        {
            Message = $"Retrieved LLM artifacts for build {buildId} (mock)",
            Result = new
            {
                buildId,
                artifacts = new[]
                {
                    new { name = "log-analysis.json", sizeBytes = 4096 },
                    new { name = "failed-tests.json", sizeBytes = 2048 }
                }
            }
        };
    }
}
