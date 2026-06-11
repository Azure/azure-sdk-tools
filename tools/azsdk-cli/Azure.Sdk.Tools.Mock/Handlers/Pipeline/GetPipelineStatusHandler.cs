// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Mock.Handlers.Pipeline;

/// <summary>
/// Mock handler for azsdk_get_pipeline_status.
/// Switches on buildId — returns a succeeded pipeline status for known IDs, default otherwise.
/// </summary>
public class GetPipelineStatusHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_pipeline_status";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var buildId = arguments?.GetValueOrDefault("buildId")?.ToString() ?? "0";

        return buildId switch
        {
            "90001" => SucceededPipelineResponse(buildId),
            _ => MockToolFactory.GetDefaultResponse()
        };
    }

    private static DefaultCommandResponse SucceededPipelineResponse(string buildId) => new()
    {
        Message = "Pipeline completed successfully",
        Result = new
        {
            buildId,
            status = "Succeeded",
            result = "succeeded",
            url = $"https://dev.azure.com/azure-sdk/internal/_build/results?buildId={buildId}"
        }
    };
}
