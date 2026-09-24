// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_create_release_plan.
/// Previews a fixed public spec snapshot before accepting an explicitly confirmed target.
/// </summary>
public class CreateReleasePlanHandler : IMockToolHandler
{
    public string ToolName => "azsdk_create_release_plan";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        if (!ReleasePlanMockResponses.IsContosoProject(ReleasePlanMockResponses.Argument(arguments, "typeSpecProjectPath")))
        {
            return new ReleasePlanResponse { ResponseError = "No release plan fixture exists for this TypeSpec project." };
        }
        if (!ApiReleaseTypeExtensions.TryParseFromUserInput(ReleasePlanMockResponses.Argument(arguments, "apiReleaseType"), out var releaseType))
        {
            return new ReleasePlanResponse { ResponseError = "Invalid API release type. Supported values are: Private Preview, Public Preview, GA" };
        }
        var pr = ReleasePlanMockResponses.Argument(arguments, "specPullRequestUrl");
        var error = releaseType.ValidateSpecPullRequest(pr);
        if (error != null)
        {
            return new ReleasePlanResponse { ResponseError = error };
        }
        var sdkReleaseType = releaseType.GetDefaultSdkReleaseType();
        ReleasePlanSpecTarget? target = null;
        if (!string.IsNullOrWhiteSpace(pr) && releaseType != ApiReleaseType.PrivatePreview)
        {
            error = ReleasePlanMockResponses.GetTarget(arguments, sdkReleaseType, null, out target);
            if (error != null)
            {
                return new ReleasePlanResponse { ResponseError = error };
            }
            if (ReleasePlanMockResponses.NeedsConfirmation(arguments, target!))
            {
                return ReleasePlanMockResponses.Preview(target!);
            }
        }
        else if (!string.IsNullOrWhiteSpace(ReleasePlanMockResponses.Argument(arguments, "apiVersion")) ||
            !string.IsNullOrWhiteSpace(ReleasePlanMockResponses.Argument(arguments, "specCommitSha")))
        {
            return new ReleasePlanResponse { ResponseError = "A public spec PR is required to validate and confirm an SDK release target. Create a tracking-only plan without version/commit inputs until the PR is available." };
        }

        var plan = releaseType == ApiReleaseType.PrivatePreview ? ReleasePlanMockResponses.PlanForId("35002")! :
            target == null ? ReleasePlanMockResponses.PlanForId("35003")! : ReleasePlanMockResponses.PlanForPullRequest(pr)!;
        plan.ApiReleaseType = releaseType;
        plan.SDKReleaseType = sdkReleaseType;
        plan.ActiveSpecPullRequest = pr;
        plan.SDKReleaseMonth = ReleasePlanMockResponses.Argument(arguments, "targetReleaseMonthYear");
        plan.ServiceTreeId = ReleasePlanMockResponses.Argument(arguments, "serviceTreeId");
        plan.ProductTreeId = ReleasePlanMockResponses.Argument(arguments, "productTreeId");
        plan.IsTestReleasePlan = ReleasePlanMockResponses.Flag(arguments, "isTestReleasePlan");
        if (target != null)
        {
            ReleasePlanMockResponses.ApplyTarget(plan, target);
        }
        var response = ReleasePlanMockResponses.PlanResponse(plan, "Release plan created successfully (mock)");
        response.ProposedSpecTarget = target;
        if (string.IsNullOrWhiteSpace(pr))
        {
            response.Warnings = null;
            response.NextSteps = null;
        }
        return response;
    }
}
