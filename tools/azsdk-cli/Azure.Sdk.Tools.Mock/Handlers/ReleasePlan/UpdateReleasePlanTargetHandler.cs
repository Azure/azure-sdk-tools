// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_update_release_plan_target.
/// Switches on workItem ID — returns an updated response for "35000", default otherwise.
/// </summary>
public class UpdateReleasePlanTargetHandler : IMockToolHandler
{
    public string ToolName => "azsdk_update_release_plan_target";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var workItemId = arguments?.GetValueOrDefault("workItemId")?.ToString() ?? "0";
        var targetMonth = arguments?.GetValueOrDefault("targetReleaseMonthYear")?.ToString() ?? "";

        return workItemId switch
        {
            "35000" => UpdatedTargetResponse(workItemId, targetMonth),
            _ => MockToolFactory.GetDefaultResponse()
        };
    }

    private static DefaultCommandResponse UpdatedTargetResponse(string workItemId, string targetMonth) => new()
    {
        Message = $"SDK release target month updated to {targetMonth} for release plan work item {workItemId}"
    };
}
