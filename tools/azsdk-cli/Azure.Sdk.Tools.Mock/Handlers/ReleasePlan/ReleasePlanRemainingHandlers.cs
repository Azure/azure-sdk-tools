// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

internal static class ReleasePlanMockResponses
{
    public static ReleasePlanWorkItem ContosoWorkItem(string? typespecPath = null, string? releaseMonth = null) => new()
    {
        WorkItemId = 35000,
        Title = "Release Plan - Contoso.WidgetManager",
        Status = "Active",
        Owner = "testuser@microsoft.com",
        SDKReleaseMonth = releaseMonth ?? "06/2026",
        ReleasePlanId = 50001,
        IsDataPlane = true,
        SpecType = "TypeSpec",
        ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/12345",
        APISpecProjectPath = typespecPath ?? "specification/contosowidgetmanager/Contoso.WidgetManager",
        SDKReleaseType = "beta",
        SDKInfo =
        [
            new SDKInfo { Language = ".NET", PackageName = "Azure.Template.Contoso" },
            new SDKInfo { Language = "Python", PackageName = "azure-contoso-widgetmanager" }
        ]
    };

    public static ReleaseWorkflowResponse Workflow(string status, params string[] details) => new()
    {
        Language = SdkLanguage.DotNet,
        Status = status,
        TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
        Details = details.ToList()
    };
}

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
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new ReleasePlanResponse
    {
        TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
        PackageType = SdkType.Dataplane,
        Message = "Release plan updated successfully (mock)",
        ReleasePlanDetails = ReleasePlanMockResponses.ContosoWorkItem()
    };
}

/// <summary>Mock handler for azsdk_get_release_plan_for_spec_pr.</summary>
public class GetReleasePlanForSpecPrHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_release_plan_for_spec_pr";
    // Deterministic "not found" — keeps the create-release-plan flow honest in
    // eval scenarios. Stimuli that target an existing plan pass the work-item
    // ID directly and call azsdk_get_release_plan instead. See #15948.
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new ReleasePlanResponse
    {
        TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
        PackageType = SdkType.Dataplane,
        Message = "No release plan found for the given spec PR (mock)",
        ReleasePlanDetails = null
    };
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
    public CommandResponse Handle(Dictionary<string, object?>? arguments) =>
        ReleasePlanMockResponses.Workflow(
            "Updated",
            "API spec pull request URL updated on release plan (mock)");
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

