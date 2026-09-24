// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>Mock handler for azsdk_abandon_release_plan.</summary>
public class AbandonReleasePlanHandler : IMockToolHandler
{
    public string ToolName => "azsdk_abandon_release_plan";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        ReleasePlanMockResponses.Workflow("Abandoned", "Release plan abandoned (mock)");
}

/// <summary>Mock handler for azsdk_update_release_plan.</summary>
public class UpdateReleasePlanHandler : IMockToolHandler
{
    public string ToolName => "azsdk_update_release_plan";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var plan = ReleasePlanMockResponses.FindPlan(arguments);
        if (plan == null)
        {
            return new ReleasePlanResponse { ResponseError = "No active release plan fixture found." };
        }
        var sdkReleaseType = ReleasePlanMockResponses.Argument(arguments, "sdkReleaseType").ToLowerInvariant() switch
        {
            "ga" => "stable",
            "preview" => "beta",
            var value => value
        };
        if (sdkReleaseType is not ("beta" or "stable"))
        {
            return new ReleasePlanResponse { ResponseError = "Invalid SDK release type. Supported release types are: beta, stable" };
        }
        if (!ReleasePlanMockResponses.IsContosoProject(ReleasePlanMockResponses.Argument(arguments, "typeSpecProjectPath")))
        {
            return new ReleasePlanResponse { ResponseError = "The selected TypeSpec project does not match the release plan." };
        }
        var pr = ReleasePlanMockResponses.Argument(arguments, "specPullRequestUrl");
        var error = ReleasePlanMockResponses.ValidateExpectedPin(arguments, plan) ??
            ReleasePlanMockResponses.ValidateExpectedRevision(arguments, plan) ?? plan.ApiReleaseType.ValidateSpecPullRequest(pr);
        if (error != null)
        {
            return new ReleasePlanResponse { ResponseError = error };
        }
        ReleasePlanSpecTarget? target = null;
        if (!string.IsNullOrWhiteSpace(pr) && plan.ApiReleaseType != ApiReleaseType.PrivatePreview)
        {
            error = ReleasePlanMockResponses.GetTarget(arguments, sdkReleaseType, plan, out target);
            if (error != null)
            {
                return new ReleasePlanResponse { ResponseError = error };
            }
            if (ReleasePlanMockResponses.NeedsConfirmation(arguments, target!) ||
                string.IsNullOrWhiteSpace(ReleasePlanMockResponses.Argument(arguments, "expectedTargetRevision")))
            {
                return ReleasePlanMockResponses.Preview(target!, plan);
            }
            ReleasePlanMockResponses.ApplyTarget(plan, target!);
        }
        else if (string.IsNullOrWhiteSpace(pr) && sdkReleaseType != plan.SDKReleaseType)
        {
            return new ReleasePlanResponse { ResponseError = "Changing the SDK release type requires previewing and confirming the spec target. Provide the linked spec PR, API version and commit SHA." };
        }
        else if (!string.IsNullOrWhiteSpace(pr))
        {
            plan.ActiveSpecPullRequest = pr;
            plan.SpecCommitSHA = string.Empty;
        }
        plan.SDKReleaseType = sdkReleaseType;
        var response = ReleasePlanMockResponses.PlanResponse(plan, "Release plan updated successfully (mock)");
        response.ProposedSpecTarget = target;
        return response;
    }
}

/// <summary>Mock handler for azsdk_check_api_spec_ready_for_sdk.</summary>
public class CheckApiSpecReadyForSdkHandler : IMockToolHandler
{
    public string ToolName => "azsdk_check_api_spec_ready_for_sdk";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        ReleasePlanMockResponses.Workflow("Ready", "API spec is signed off and ready for SDK generation (mock)");
}

/// <summary>Mock handler for azsdk_get_kpi_attestation_status.</summary>
public class GetKpiAttestationStatusHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_kpi_attestation_status";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new ReleasePlanListResponse
    {
        ReleasePlanDetailsList = [ReleasePlanMockResponses.ContosoWorkItem()],
        Message = "All required KPIs attested for this release (mock)."
    };
}

/// <summary>Mock handler for azsdk_get_service_details_by_typespec_path.</summary>
public class GetServiceDetailsByTypeSpecPathHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_service_details_by_typespec_path";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new ProductInfoResponse
    {
        ProductInfo = new ProductInfo
        {
            ProductServiceTreeId = "00000000-0000-0000-0000-000000000099",
            ServiceId = "00000000-0000-0000-0000-000000000042",
            PackageDisplayName = "Azure SDK for Contoso WidgetManager",
            ProductServiceTreeLink = "https://servicetree.example.com/products/00000000-0000-0000-0000-000000000099",
            WorkItemId = 36000,
            Title = "Contoso.WidgetManager"
        },
        Message = "Product details resolved from TypeSpec path (mock)."
    };
}

/// <summary>Mock handler for azsdk_update_api_spec_pull_request_in_release_plan.</summary>
public class UpdateApiSpecPullRequestInReleasePlanHandler : IMockToolHandler
{
    public string ToolName => "azsdk_update_api_spec_pull_request_in_release_plan";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var id = ReleasePlanMockResponses.Argument(arguments, "releasePlanId");
        if (string.IsNullOrWhiteSpace(id) || id == "0")
        {
            id = ReleasePlanMockResponses.Argument(arguments, "workItemId");
        }
        var plan = ReleasePlanMockResponses.PlanForId(id);
        if (plan == null)
        {
            return new ReleaseWorkflowResponse { Status = "Failed", ResponseError = "Provide a known release plan work item ID or release plan ID." };
        }
        var pr = ReleasePlanMockResponses.Argument(arguments, "specPullRequestUrl");
        var error = ReleasePlanMockResponses.ValidateExpectedPin(arguments, plan) ??
            ReleasePlanMockResponses.ValidateExpectedRevision(arguments, plan) ?? plan.ApiReleaseType.ValidateSpecPullRequest(pr);
        if (error != null || string.IsNullOrWhiteSpace(pr))
        {
            return new ReleaseWorkflowResponse { Status = "Failed", ResponseError = error ?? "A spec pull request URL is required." };
        }
        if (plan.ApiReleaseType == ApiReleaseType.PrivatePreview)
        {
            return ReleasePlanMockResponses.Workflow("Success", $"Successfully updated spec pull request URL to {pr} in release plan (mock).", "Private-preview release plans do not require an SDK generation commit pin.");
        }
        error = ReleasePlanMockResponses.GetTarget(arguments, plan.SDKReleaseType, plan, out var target);
        if (error != null)
        {
            return new ReleaseWorkflowResponse { Status = "Failed", ResponseError = error };
        }
        if (ReleasePlanMockResponses.NeedsConfirmation(arguments, target!) ||
            string.IsNullOrWhiteSpace(ReleasePlanMockResponses.Argument(arguments, "expectedTargetRevision")))
        {
            return new ReleaseWorkflowResponse
            {
                Status = "Confirmation required",
                ProposedSpecTarget = target,
                RequiresConfirmation = true,
                NextSteps = [ReleasePlanMockResponses.ConfirmationNextStep]
            };
        }
        var response = ReleasePlanMockResponses.Workflow("Success", $"Successfully updated spec pull request URL to {pr} in release plan (mock).", $"Pinned spec commit SHA: {target!.SpecCommitSHA}.");
        response.ProposedSpecTarget = target;
        return response;
    }
}

/// <summary>Mock handler for azsdk_update_language_exclusion_justification.</summary>
public class UpdateLanguageExclusionJustificationHandler : IMockToolHandler
{
    public string ToolName => "azsdk_update_language_exclusion_justification";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new DefaultCommandResponse
    {
        Message = "Updated language exclusion justification in release plan (mock)."
    };
}

