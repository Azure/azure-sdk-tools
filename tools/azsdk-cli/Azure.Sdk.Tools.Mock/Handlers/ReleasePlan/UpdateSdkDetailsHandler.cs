// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_update_sdk_details_in_release_plan.
/// Switches on workItem ID — returns an updated response for "35000", default otherwise.
/// </summary>
public class UpdateSdkDetailsHandler : IMockToolHandler
{
    public string ToolName => "azsdk_update_sdk_details_in_release_plan";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var workItemId = arguments?.GetValueOrDefault("releasePlanWorkItemId")?.ToString() ?? "0";

        return workItemId switch
        {
            "35000" => UpdatedDetailsResponse(workItemId),
            _ => MockToolFactory.GetDefaultResponse()
        };
    }

    private static DefaultCommandResponse UpdatedDetailsResponse(string workItemId) => new()
    {
        Message = $"SDK details updated in release plan work item {workItemId}"
    };
}
