// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_run_generate_sdk.
/// Generates only from a known, pinned fixture; optional target inputs are guards, not overrides.
/// </summary>
public class RunGenerateSdkHandler : IMockToolHandler
{
    public string ToolName => "azsdk_run_generate_sdk";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var plan = ReleasePlanMockResponses.PlanForId(ReleasePlanMockResponses.Argument(arguments, "workItemId"));
        if (plan == null)
        {
            return Failure("A known release plan work item ID is required to run SDK generation.");
        }
        if (plan.ApiReleaseType == ApiReleaseType.PrivatePreview)
        {
            return ReleasePlanMockResponses.Workflow("Success", "Private Preview is spec-only. Merge the spec PR; no SDK generation pipeline was triggered.");
        }
        if (plan.SpecCommitSHA.Length != 40 || !plan.SpecCommitSHA.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(plan.SpecAPIVersion))
        {
            return Failure("The release plan has a missing or invalid spec commit SHA or API version. Preview and confirm the release target with update-spec-pr before generating SDKs; generation never chooses a new target implicitly.");
        }
        if (!ReleasePlanMockResponses.IsContosoProject(ReleasePlanMockResponses.Argument(arguments, "typespecProjectRoot")))
        {
            return Failure("The TypeSpec project does not match the release plan's confirmed target.");
        }
        if (!string.Equals(ReleasePlanMockResponses.Argument(arguments, "sdkReleaseType"), plan.SDKReleaseType, StringComparison.OrdinalIgnoreCase))
        {
            return Failure("SDK release type does not match the release plan's confirmed target.");
        }
        var apiVersion = ReleasePlanMockResponses.Argument(arguments, "apiVersion");
        if (!string.IsNullOrWhiteSpace(apiVersion) && !apiVersion.Equals("none", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(apiVersion, plan.SpecAPIVersion, StringComparison.OrdinalIgnoreCase))
        {
            return Failure("API version does not match the release plan's confirmed target.");
        }
        var sha = ReleasePlanMockResponses.Argument(arguments, "specCommitSha");
        if (!string.IsNullOrEmpty(sha) && !string.Equals(sha, plan.SpecCommitSHA, StringComparison.OrdinalIgnoreCase))
        {
            return Failure("The release plan's spec commit changed. Retrieve and review the current target before generating.");
        }
        var prNumber = ReleasePlanMockResponses.Argument(arguments, "pullRequestNumber");
        if (!string.IsNullOrWhiteSpace(prNumber) && prNumber != "0" && prNumber != new Uri(plan.ActiveSpecPullRequest).Segments.Last())
        {
            return Failure("Spec PR does not match the release plan's linked PR. Update and confirm the spec target before regenerating.");
        }
        var language = SdkLanguageHelpers.GetSdkLanguage(ReleasePlanMockResponses.Argument(arguments, "language"));
        if (!plan.SDKInfo.Any(sdk => SdkLanguageHelpers.GetSdkLanguage(sdk.Language) == language))
        {
            return Failure("Specify one SDK language listed in the release plan per generation call.");
        }
        var requireMergedSpec = ReleasePlanMockResponses.Flag(arguments, "requireMergedSpec");
        if (requireMergedSpec && plan.SpecCommitSHA != ReleasePlanMockResponses.MergedSpecCommitSha)
        {
            return Failure("Auto-release generation requires the confirmed target to use the linked PR's merge commit. Preview and confirm the target after merge; pre-merge draft SDK review remains available without requireMergedSpec.");
        }
        var response = ReleasePlanMockResponses.Workflow("Success",
            $"SDK generation uses pinned spec commit {plan.SpecCommitSHA} and API version '{plan.SpecAPIVersion}' for work item {plan.WorkItemId}.",
            requireMergedSpec ? "SDK release generation pipeline triggered (mock)." : "Draft SDK review generation pipeline triggered (mock); no auto-release labels.",
            "Pipeline build ID: 90001",
            "Monitor status using azsdk_get_pipeline_status");
        response.Language = language;
        return response;
    }

    private static ReleaseWorkflowResponse Failure(string error) => new()
    {
        Status = "Failed",
        ResponseError = error
    };
}
