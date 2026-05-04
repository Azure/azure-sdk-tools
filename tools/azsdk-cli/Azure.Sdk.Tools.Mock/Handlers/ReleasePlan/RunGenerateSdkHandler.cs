// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_run_generate_sdk.
/// Switches on apiVersion — returns a queued pipeline response for the expected version, default otherwise.
/// </summary>
public class RunGenerateSdkHandler : IMockToolHandler
{
    public string ToolName => "azsdk_run_generate_sdk";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var apiVersion = arguments?.GetValueOrDefault("apiVersion")?.ToString() ?? "";

        return apiVersion switch
        {
            "2024-01-01-preview" => QueuedPipelineResponse(),
            _ => MockToolFactory.GetDefaultResponse()
        };
    }

    private static ReleaseWorkflowResponse QueuedPipelineResponse() => new()
    {
        Language = SdkLanguage.DotNet,
        Status = "Queued",
        TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
        Details =
        [
            "SDK generation pipeline triggered",
            "Pipeline build ID: 90001",
            "Monitor status using azsdk_get_pipeline_status"
        ]
    };
}
