// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_run_generate_sdk.
/// Generates from the same saved target returned by the release-plan lookup fixture.
/// </summary>
public class RunGenerateSdkHandler : IMockToolHandler
{
    public string ToolName => "azsdk_run_generate_sdk";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var workItemId = arguments?.GetValueOrDefault("workItemId")?.ToString() ?? "0";
        var plan = (new GetReleasePlanHandler().Handle(new Dictionary<string, object?>
        {
            ["workItemId"] = workItemId,
            ["releasePlanId"] = workItemId
        }) as ReleasePlanResponse)?.ReleasePlanDetails;
        if (plan == null)
        {
            return MockToolFactory.GetDefaultResponse();
        }

        var projectPath = arguments?.GetValueOrDefault("typespecProjectRoot")?.ToString() ?? "";
        var releaseType = arguments?.GetValueOrDefault("sdkReleaseType")?.ToString() ?? "";
        var apiVersion = arguments?.GetValueOrDefault("apiVersion")?.ToString() ?? "";
        var pullRequestNumber = arguments?.GetValueOrDefault("pullRequestNumber")?.ToString() ?? "0";
        var language = SdkLanguageHelpers.GetSdkLanguage(arguments?.GetValueOrDefault("language")?.ToString() ?? "");

        if (!string.Equals(projectPath.Replace('\\', '/').TrimEnd('/'), plan.APISpecProjectPath, StringComparison.Ordinal) ||
            !string.Equals(releaseType, plan.SDKReleaseType, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(apiVersion) && !apiVersion.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(apiVersion, plan.SpecAPIVersion, StringComparison.OrdinalIgnoreCase)) ||
            (pullRequestNumber != "0" && !string.Equals(plan.ActiveSpecPullRequest,
                $"https://github.com/Azure/azure-rest-api-specs/pull/{pullRequestNumber}", StringComparison.OrdinalIgnoreCase)))
        {
            return new ReleaseWorkflowResponse
            {
                Status = "Failed",
                ResponseError = "Generation inputs do not match the release plan's stored target. Read the plan and use its saved project, API version, SDK release type and linked spec PR."
            };
        }
        if (!plan.SDKInfo.Any(sdk => SdkLanguageHelpers.GetSdkLanguage(sdk.Language) == language))
        {
            return new ReleaseWorkflowResponse { Status = "Failed", ResponseError = "The requested language has no SDK details in the release plan." };
        }

        return new ReleaseWorkflowResponse
        {
            Language = language,
            Status = "Queued",
            TypeSpecProject = plan.APISpecProjectPath,
            Details =
            [
                $"SDK generation uses pinned spec commit {plan.SpecCommitSHA} and API version '{plan.SpecAPIVersion}' with SDK release type '{plan.SDKReleaseType}'.",
                "SDK generation pipeline triggered",
                "Pipeline build ID: 90001",
                "Monitor status using azsdk_get_pipeline_status"
            ]
        };
    }
}
