// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_link_sdk_pull_request_to_release_plan.
/// Switches on pullRequestUrl — returns a linked response for the expected PR, default otherwise.
/// </summary>
public class LinkSdkPrToReleasePlanHandler : IMockToolHandler
{
    public string ToolName => "azsdk_link_sdk_pull_request_to_release_plan";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var prUrl = arguments?.GetValueOrDefault("pullRequestUrl")?.ToString() ?? "";

        return prUrl switch
        {
            "https://github.com/Azure/azure-sdk-for-net/pull/45001" => LinkedPrResponse(prUrl),
            _ => MockToolFactory.GetDefaultResponse()
        };
    }

    private static ReleaseWorkflowResponse LinkedPrResponse(string prUrl) => new()
    {
        Language = SdkLanguage.DotNet,
        Status = "Linked",
        TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
        Details =
        [
            "SDK pull request linked to release plan",
            $"PR: {prUrl}"
        ]
    };
}
