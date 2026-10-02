// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

internal static class ReleasePlanMockResponses
{
    internal const string ContosoTypeSpecProjectPath = "specification/contosowidgetmanager/Contoso.WidgetManager";
    internal const string ContosoApiVersion = "2024-01-01";
    internal const string DefaultSpecPullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/38387";
    internal const string SpecSha = "0123456789abcdef0123456789abcdef01234567";
    internal const int WorkflowWorkItemId = 29262;

    public static ReleasePlanWorkItem ContosoWorkItem(string? typespecPath = null, string? releaseMonth = null, int workItemId = 35000) => new()
    {
        WorkItemId = workItemId,
        Title = "Release Plan - Contoso.WidgetManager",
        Status = "Active",
        Owner = "testuser@microsoft.com",
        SDKReleaseMonth = releaseMonth ?? "December 2026",
        ReleasePlanId = workItemId == WorkflowWorkItemId ? WorkflowWorkItemId : 50001,
        ApiReleaseType = ApiReleaseType.GA,
        SpecCommitSHA = SpecSha,
        SpecAPIVersion = ContosoApiVersion,
        IsDataPlane = true,
        SpecType = "TypeSpec",
        ActiveSpecPullRequest = DefaultSpecPullRequestUrl,
        APISpecProjectPath = typespecPath ?? ContosoTypeSpecProjectPath,
        SDKReleaseType = "beta",
        SDKInfo =
        [
            new SDKInfo { Language = ".NET", PackageName = "Azure.Template.Contoso" },
            new SDKInfo { Language = "Python", PackageName = "azure-contoso-widgetmanager" },
            new SDKInfo { Language = "JavaScript", PackageName = "@azure/contoso-widgetmanager" },
            new SDKInfo { Language = "Java", PackageName = "azure-contoso-widgetmanager" }
        ]
    };

    internal static string NormalizeProjectPath(string path)
    {
        path = path.Replace('\\', '/').TrimEnd('/');
        if (path.EndsWith("/tspconfig.yaml", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^"/tspconfig.yaml".Length];
        }
        var specificationIndex = path.IndexOf("/specification/", StringComparison.OrdinalIgnoreCase);
        if (specificationIndex >= 0 && !path.Contains("://", StringComparison.Ordinal))
        {
            path = path[(specificationIndex + 1)..];
        }
        return path;
    }

    // Canned metadata and PR source commit only; writes do not mutate the lookup fixture.
    public static ReleasePlanResponse SaveTarget(Dictionary<string, object?>? arguments, bool update, ReleasePlanResponse? response = null)
    {
        string Argument(string key) => arguments?.GetValueOrDefault(key)?.ToString() ?? string.Empty;
        var workItemId = 35000;
        if (update)
        {
            if (!int.TryParse(Argument("workItemId"), out workItemId) || workItemId <= 0)
            {
                return new ReleasePlanResponse { ResponseError = "A positive work item ID is required. Look up the release plan first and use its WorkItemId." };
            }
            if (workItemId != 35000 && workItemId != WorkflowWorkItemId)
            {
                return new ReleasePlanResponse { ResponseError = $"No release plan found for work item ID {workItemId}. No other plan was selected." };
            }
        }

        var plan = response?.ReleasePlanDetails ?? ContosoWorkItem(workItemId: workItemId);
        var path = NormalizeProjectPath(Argument("typeSpecProjectPath"));
        if (!string.Equals(path, plan.APISpecProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            return new ReleasePlanResponse { ResponseError = "Provide the local Contoso TypeSpec project at the selected commit." };
        }

        var specPr = Argument("specPullRequestUrl").Trim();
        if (string.IsNullOrWhiteSpace(specPr))
        {
            specPr = plan.ActiveSpecPullRequest;
        }
        if (!string.IsNullOrWhiteSpace(specPr) &&
            !Regex.IsMatch(specPr, @"^https://github\.com/Azure/azure-rest-api-specs(-pr)?/pull/[1-9][0-9]*/?$", RegexOptions.IgnoreCase))
        {
            return new ReleasePlanResponse { ResponseError = "Provide a valid GitHub pull request URL in Azure/azure-rest-api-specs or Azure/azure-rest-api-specs-pr." };
        }
        var releaseType = plan.ApiReleaseType == ApiReleaseType.Unknown ? ApiReleaseType.PublicPreview : plan.ApiReleaseType;
        if (releaseType.ValidateSpecPullRequest(specPr) is { } error)
        {
            return new ReleasePlanResponse { ResponseError = error };
        }

        var saveCommit = !string.IsNullOrWhiteSpace(specPr) && releaseType != ApiReleaseType.PrivatePreview;
        var sha = Argument("specCommitSha");
        if (!string.IsNullOrEmpty(sha) && !saveCommit)
        {
            return new ReleasePlanResponse { ResponseError = "A public spec PR is required to save an SDK generation commit." };
        }
        if (!string.IsNullOrEmpty(sha) && !string.Equals(sha, SpecSha, StringComparison.OrdinalIgnoreCase))
        {
            return new ReleasePlanResponse { ResponseError = "The spec commit does not match the fixture PR source commit. Use its source commit or omit specCommitSha." };
        }

        var sdkReleaseType = plan.SDKReleaseType;
        if (update && !string.IsNullOrWhiteSpace(Argument("sdkReleaseType")))
        {
            sdkReleaseType = Argument("sdkReleaseType").ToLowerInvariant() switch
            {
                "ga" => "stable",
                "preview" => "beta",
                var value => value
            };
            if (sdkReleaseType is not ("beta" or "stable"))
            {
                return new ReleasePlanResponse { ResponseError = "Invalid SDK release type. Supported release types are: beta, stable" };
            }
        }

        plan.SpecCommitSHA = saveCommit ? SpecSha : string.Empty;
        if (releaseType != ApiReleaseType.PrivatePreview)
        {
            plan.SpecAPIVersion = ContosoApiVersion;
        }
        plan.ActiveSpecPullRequest = specPr;
        plan.SDKReleaseType = sdkReleaseType;
        response ??= new ReleasePlanResponse { TypeSpecProject = plan.APISpecProjectPath, PackageType = SdkType.Dataplane };
        response.Message = update ? "Release target saved (mock)." : "Release plan created successfully";
        response.ReleasePlanDetails = plan;
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
    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments?.GetValueOrDefault("sdkReleaseType")?.ToString()))
        {
            return new ReleasePlanResponse { ResponseError = "Invalid SDK release type. Supported release types are: beta, stable" };
        }
        return ReleasePlanMockResponses.SaveTarget(arguments, update: true);
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
        if (string.IsNullOrWhiteSpace(arguments?.GetValueOrDefault("specPullRequestUrl")?.ToString()))
        {
            return new ReleaseWorkflowResponse { ResponseError = "API spec pull request URL is required for this release plan operation." };
        }
        var response = ReleasePlanMockResponses.SaveTarget(arguments, update: true);
        return new ReleaseWorkflowResponse
        {
            Status = response.OperationStatus == Status.Failed ? "Failed" : "Success",
            ResponseError = response.ResponseError,
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

