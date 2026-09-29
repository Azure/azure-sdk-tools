// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

internal static class ReleasePlanMockResponses
{
    private const string SpecSha = "0123456789abcdef0123456789abcdef01234567";
    private const string Revision = "35000:1:45000:1";

    public static ReleasePlanWorkItem ContosoWorkItem(string? typespecPath = null, string? releaseMonth = null) => new()
    {
        WorkItemId = 35000,
        Title = "Release Plan - Contoso.WidgetManager",
        Status = "Active",
        Owner = "testuser@microsoft.com",
        SDKReleaseMonth = releaseMonth ?? "December 2026",
        ReleasePlanId = 50001,
        ApiSpecWorkItemId = 45000,
        TargetRevision = Revision,
        SpecCommitSHA = SpecSha,
        SpecAPIVersion = "2024-01-01",
        IsDataPlane = true,
        SpecType = "TypeSpec",
        ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/12345",
        APISpecProjectPath = typespecPath ?? "specification/contosowidgetmanager/Contoso.WidgetManager",
        SDKReleaseType = "beta",
        SDKInfo =
        [
            new SDKInfo { Language = ".NET", PackageName = "Azure.Template.Contoso" },
            new SDKInfo { Language = "Python", PackageName = "azure-contoso-widgetmanager" },
            new SDKInfo { Language = "JavaScript", PackageName = "@azure/contoso-widgetmanager" },
            new SDKInfo { Language = "Java", PackageName = "azure-contoso-widgetmanager" }
        ]
    };

    // One canned target, not a work-item store. Real service tests cover revision conflicts and partial writes.
    public static ReleasePlanResponse ConfigureTarget(Dictionary<string, object?>? arguments, bool update, ReleasePlanResponse? response = null)
    {
        string Argument(string key) => arguments?.GetValueOrDefault(key)?.ToString() ?? string.Empty;
        var plan = response?.ReleasePlanDetails ?? ContosoWorkItem();
        var path = Argument("typeSpecProjectPath").Replace('\\', '/').TrimEnd('/');
        if (path.EndsWith("/tspconfig.yaml", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^"/tspconfig.yaml".Length];
        }
        var specificationIndex = path.IndexOf("/specification/", StringComparison.OrdinalIgnoreCase);
        if (specificationIndex >= 0 && !path.Contains("://", StringComparison.Ordinal))
        {
            path = path[(specificationIndex + 1)..];
        }
        if (!string.Equals(path, plan.APISpecProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            return new ReleasePlanResponse { ResponseError = "Provide the local Contoso TypeSpec project at the selected commit." };
        }
        var version = Argument("apiVersion");
        var sha = Argument("specCommitSha");
        var revision = Argument("expectedTargetRevision");
        if (ApiReleaseType.PublicPreview.ValidateSpecPullRequest(Argument("specPullRequestUrl")) is { } error)
        {
            return new ReleasePlanResponse { ResponseError = error };
        }
        if ((!string.IsNullOrEmpty(version) && version != "2024-01-01") ||
            (!string.IsNullOrEmpty(sha) && !string.Equals(sha, SpecSha, StringComparison.OrdinalIgnoreCase)))
        {
            return new ReleasePlanResponse { ResponseError = "The API version or commit does not match the fixture metadata. Preview again; do not choose a default or latest version." };
        }
        if (update && arguments?.GetValueOrDefault("expectedTargetRevision") != null && revision != Revision)
        {
            return new ReleasePlanResponse { ResponseError = "The release plan or API Spec changed. Preview again and obtain fresh approval." };
        }
        var target = new ReleasePlanSpecTarget
        {
            TypeSpecProjectPath = plan.APISpecProjectPath,
            ApiVersion = "2024-01-01", AvailableApiVersions = ["2024-01-01"],
            SpecPullRequestUrl = string.IsNullOrWhiteSpace(Argument("specPullRequestUrl")) ? plan.ActiveSpecPullRequest : Argument("specPullRequestUrl"),
            SpecCommitSHA = SpecSha, CommitUrl = $"https://github.com/Azure/azure-rest-api-specs/commit/{SpecSha}",
            SDKReleaseType = string.IsNullOrEmpty(Argument("sdkReleaseType"))
                ? (string.Equals(Argument("apiReleaseType"), "GA", StringComparison.OrdinalIgnoreCase) ? "stable" : "beta") : Argument("sdkReleaseType"),
            ExpectedTargetRevision = update ? Revision : null,
            Packages = plan.SDKInfo.Select(sdk => new PackageInfo { Language = SdkLanguageHelpers.GetSdkLanguage(sdk.Language), PackageName = sdk.PackageName, ApiVersion = "2024-01-01" }).ToList()
        };
        var confirmed = bool.TryParse(Argument("confirmTarget"), out var confirm) && confirm && sha.Length > 0 && (!update || revision == Revision);
        response ??= new ReleasePlanResponse { TypeSpecProject = path, PackageType = SdkType.Dataplane };
        response.ProposedSpecTarget = target;
        response.RequiresConfirmation = !confirmed;
        response.Message = confirmed ? "Release target saved (mock)." : "Review and confirm the release target. No release plan was created or updated.";
        response.ReleasePlanDetails = confirmed || update ? plan : null;
        if (confirmed)
        {
            plan.SpecCommitSHA = SpecSha;
            plan.SpecAPIVersion = target.ApiVersion;
            plan.ActiveSpecPullRequest = target.SpecPullRequestUrl;
            plan.SDKReleaseType = target.SDKReleaseType;
            plan.ApiSpecWorkItemId = 45000;
            plan.TargetRevision = Revision;
        }
        else
        {
            (response.NextSteps ??= []).Add("Show the proposed target and ask for approval. Repeat with specCommitSha and confirmTarget=true; updates also require the preview's ExpectedTargetRevision as expectedTargetRevision. Do not generate SDKs as part of confirmation.");
        }
        return response;
    }

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
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => ReleasePlanMockResponses.ConfigureTarget(arguments, update: true);
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
        var response = ReleasePlanMockResponses.ConfigureTarget(arguments, update: true);
        return new ReleaseWorkflowResponse
        {
            Status = response.OperationStatus == Status.Failed ? "Failed" : response.RequiresConfirmation ? "Confirmation required" : "Success",
            ResponseError = response.ResponseError,
            RequiresConfirmation = response.RequiresConfirmation,
            ProposedSpecTarget = response.ProposedSpecTarget,
            NextSteps = response.NextSteps,
            Details = [response.Message ?? string.Empty]
        };
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

