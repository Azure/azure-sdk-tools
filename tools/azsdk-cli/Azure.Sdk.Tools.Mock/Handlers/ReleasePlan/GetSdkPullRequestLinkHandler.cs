// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_get_sdk_pull_request_link.
/// Switches on workItemId — returns a PR link response for the expected work item, default otherwise.
/// </summary>
public class GetSdkPullRequestLinkHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_sdk_pull_request_link";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var workItemId = arguments?.GetValueOrDefault("workItemId")?.ToString() ?? "0";

        return workItemId switch
        {
            "35000" => CompletedPrLinkResponse(),
            _ => MockToolFactory.GetDefaultResponse()
        };
    }

    private static ReleaseWorkflowResponse CompletedPrLinkResponse() => new()
    {
        Language = SdkLanguage.DotNet,
        Status = "Completed",
        TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
        Details =
        [
            "SDK pull request created",
            "PR URL: https://github.com/Azure/azure-sdk-for-net/pull/45001"
        ]
    };
}
