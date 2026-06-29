// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Mock.Handlers.Pipeline;

/// <summary>Mock handler for azsdk_analyze_pipeline. Returns a fake-build summary with a single failed test + task.</summary>
public class AnalyzePipelineHandler : IMockToolHandler
{
    public string ToolName => "azsdk_analyze_pipeline";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var buildId = arguments?.GetValueOrDefault("buildId")?.ToString() ?? "90001";
        return new AnalyzePipelineResponse
        {
            FailedTests = new Dictionary<string, List<string>>
            {
                ["Contoso.Widgets.Tests"] = ["WidgetClientLiveTests.GetWidget"]
            },
            FailedTasks =
            [
                new LogAnalysisResponse
                {
                    PipelineUrl = $"https://dev.azure.com/azure-sdk/internal/_build/results?buildId={buildId}",
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
            ]
        };
    }
}

/// <summary>Mock handler for azsdk_get_pipeline_llm_artifacts.</summary>
public class GetPipelineLlmArtifactsHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_pipeline_llm_artifacts";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var buildId = arguments?.GetValueOrDefault("buildId")?.ToString() ?? "90001";
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
