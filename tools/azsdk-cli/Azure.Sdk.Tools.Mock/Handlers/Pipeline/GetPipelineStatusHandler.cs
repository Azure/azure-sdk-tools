// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Mock.Handlers.Pipeline;

/// <summary>
/// Mock handler for azsdk_get_pipeline_status.
/// Switches on buildId, returning a completed pipeline status for known IDs and a default otherwise.
/// </summary>
public class GetPipelineStatusHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_pipeline_status";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var buildId = MockPipelineIdentifier.GetBuildId(arguments) ?? "0";

        return buildId switch
        {
            "90001" => CompletedPipelineResponse(buildId),
            _ => MockToolFactory.GetDefaultResponse()
        };
    }

    private static DefaultCommandResponse CompletedPipelineResponse(string buildId) => new()
    {
        Message = "Pipeline completed with failures",
        Result = new[]
        {
            new
            {
                build_id = int.TryParse(buildId, out var id) ? id : 0,
                project = "internal",
                pipeline_url = $"https://dev.azure.com/azure-sdk/internal/_build/results?buildId={buildId}",
                status = "completed",
                result = "failed"
            }
        }
    };
}
