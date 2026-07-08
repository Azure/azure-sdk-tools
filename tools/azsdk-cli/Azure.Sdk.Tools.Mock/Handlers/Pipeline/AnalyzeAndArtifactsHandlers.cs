// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Mock.Handlers.Pipeline;

/// <summary>
/// Mock handler for azsdk_analyze_pipeline. Returns the canonical fixture failure:
/// the Storage QueueClientOptions / ShareClientOptions TryGetServiceVersion parser is missing the
/// two newest service-version cases, "2026-10-06" (V2026_10_06) and "2026-12-06" (V2026_12_06) —
/// a real, identifiable code bug. Because TryGetServiceVersion_ParsesAllServiceVersions iterates
/// every enum value, a complete fix must add BOTH cases; adding only one leaves the test red. This
/// is the same bug the tools/azsdk-cli/Azure.Sdk.Tools.Vally/fixtures/analyze-pipeline/QueueClientOptionsTests fixture overlays for the
/// fixer, so a single fixture drives both the analyze and fix quality evals. See
/// tools/azsdk-cli/Azure.Sdk.Tools.Vally/evals/quality/ and tools/azsdk-cli/Azure.Sdk.Tools.Vally/fixtures/analyze-pipeline/QueueClientOptionsTests.
/// </summary>
public class AnalyzePipelineHandler : IMockToolHandler
{
    public string ToolName => "azsdk_analyze_pipeline";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var buildId = arguments?.GetValueOrDefault("buildId")?.ToString() ?? "6455504";
        const string versionParseError =
            "TryGetServiceVersion failed to parse the new service versions \"2026-10-06\" (V2026_10_06) and \"2026-12-06\" (V2026_12_06): both cases are missing from the parser switch. Expected: True But was: False";
        return new AnalyzePipelineResponse
        {
            FailedTests = new Dictionary<string, List<string>>
            {
                ["azure.storage.queues.tests.dll"] =
                    ["Azure.Storage.Queues.Test.QueueClientOptionsTests.TryGetServiceVersion_ParsesAllServiceVersions"],
                ["azure.storage.files.shares.tests.dll"] =
                    ["Azure.Storage.Files.Shares.Tests.ShareClientOptionsTests.TryGetServiceVersion_ParsesAllServiceVersions"]
            },
            FailedTasks =
            [
                new LogAnalysisResponse
                {
                    PipelineUrl = $"https://dev.azure.com/azure-sdk/public/_build/results?buildId={buildId}",
                    Errors =
                    [
                        new LogEntry
                        {
                            File = "sdk/storage/Azure.Storage.Queues/tests/QueueClientOptionsTests.cs",
                            Line = 24,
                            Message = versionParseError
                        },
                        new LogEntry
                        {
                            File = "sdk/storage/Azure.Storage.Files.Shares/tests/ShareClientOptionsTests.cs",
                            Line = 24,
                            Message = versionParseError
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
