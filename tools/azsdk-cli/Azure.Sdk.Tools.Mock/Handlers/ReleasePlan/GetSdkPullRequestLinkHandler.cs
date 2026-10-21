// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_get_sdk_pull_request_link.
/// Returns the requested language's saved SDK PR for the canonical release plan.
/// </summary>
public class GetSdkPullRequestLinkHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_sdk_pull_request_link";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var workItemId = arguments?.GetValueOrDefault("workItemId")?.ToString() ?? "0";

        if (workItemId is not ("35000" or "50001"))
        {
            return MockToolFactory.GetDefaultResponse();
        }

        var language = new InputSanitizer().SanitizeLanguage(arguments?.GetValueOrDefault("language")?.ToString() ?? "");
        var plan = (ReleasePlanResponse)new GetReleasePlanHandler().Handle(new()
        {
            [workItemId == "50001" ? "releasePlanId" : "workItemId"] = workItemId
        });
        var sdkInfo = plan.ReleasePlanDetails!.SDKInfo.FirstOrDefault(s => string.Equals(s.Language, language, StringComparison.OrdinalIgnoreCase));
        var response = new ReleaseWorkflowResponse { TypeSpecProject = plan.TypeSpecProject, ResponseErrors = [] };
        response.SetLanguage(language);
        if (string.IsNullOrEmpty(sdkInfo?.SdkPullRequestUrl))
        {
            response.Status = "Failed";
            response.ResponseErrors.Add($"No SDK pull request details found for {language} in release plan work item.");
            return response;
        }

        response.Status = "Completed";
        response.Details =
        [
            "SDK pull request created",
            $"PR URL: {sdkInfo.SdkPullRequestUrl}"
        ];
        return response;
    }
}
