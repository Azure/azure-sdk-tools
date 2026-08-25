// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Pipeline;
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Mock.Handlers.Pipeline;

/// <summary>
/// Mock handler for azsdk_analyze_pipeline. Returns the canonical fixture failure:
/// the Storage QueueClientOptions / ShareClientOptions TryGetServiceVersion parser is missing the
/// two newest service-version cases, "2026-10-06" (V2026_10_06) and "2026-12-06" (V2026_12_06) —
/// a real, identifiable code bug. Because TryGetServiceVersion_ParsesAllServiceVersions iterates
/// every enum value, a complete fix must add BOTH cases; adding only one leaves the test red. This
/// is the same bug the evals/fixtures/analyze-pipeline/QueueClientOptionsTests fixture overlays for the
/// fixer, so a single fixture drives both the analyze and fix quality evals. See
/// evals/workflows/mock/ and evals/fixtures/analyze-pipeline/QueueClientOptionsTests.
/// </summary>
public class AnalyzePipelineHandler : IMockToolHandler
{
    public string ToolName => "azsdk_analyze_pipeline";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var buildId = MockPipelineIdentifier.GetBuildId(arguments) ?? "90001";
        var buildIdValue = int.TryParse(buildId, out var parsed) ? parsed : 90001;
        var pipelineUrl = $"https://dev.azure.com/azure-sdk/internal/_build/results?buildId={buildId}";
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
                                "WidgetClientLiveTests.GetWidget"
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
                                Message = "Test WidgetClientLiveTests.GetWidget failed: expected 200 got 404"
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
