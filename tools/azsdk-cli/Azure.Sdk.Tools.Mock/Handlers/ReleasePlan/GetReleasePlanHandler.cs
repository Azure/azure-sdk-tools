// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_get_release_plan.
/// Switches on workItem ID — returns the Contoso release plan for "35000", fixed
/// lifecycle-stage fixtures for "35010"-"35013" (used by multi-turn-process-workflows.eval.yaml
/// to test "what's next" reasoning), default otherwise.
/// </summary>
public class GetReleasePlanHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_release_plan";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var workItemId = arguments?.GetValueOrDefault("workItem")?.ToString() ?? "0";

        return workItemId switch
        {
            "35000" => ContosoReleasePlanResponse(),
            "35010" => SpecNotReviewedResponse(),
            "35011" => SpecApprovedNoSdkResponse(),
            "35012" => SdkGeneratedPendingReleaseResponse(),
            "35013" => FullyReleasedResponse(),
            _ => MockToolFactory.GetDefaultResponse()
        };
    }

    private static ReleasePlanResponse ContosoReleasePlanResponse() => new()
    {
        TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
        PackageType = SdkType.Dataplane,
        Message = "Release plan found",
        ReleasePlanDetails = new ReleasePlanWorkItem
        {
            WorkItemId = 35000,
            Title = "Release Plan - Contoso.WidgetManager",
            Status = "Active",
            Owner = "testuser@microsoft.com",
            SDKReleaseMonth = "06/2026",
            ReleasePlanId = 50001,
            IsDataPlane = true,
            SpecType = "TypeSpec",
            ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/12345",
            APISpecProjectPath = "specification/contosowidgetmanager/Contoso.WidgetManager",
            SDKReleaseType = "beta",
            SDKInfo =
            [
                new SDKInfo { Language = ".NET", PackageName = "Azure.Template.Contoso", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-net/pull/45001" },
                new SDKInfo { Language = "Python", PackageName = "azure-contoso-widgetmanager", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/45002" },
                new SDKInfo { Language = "JavaScript", PackageName = "@azure/contoso-widgetmanager", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-js/pull/45003" },
                new SDKInfo { Language = "Java", PackageName = "azure-contoso-widgetmanager", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-java/pull/45004" },
            ]
        }
    };

    // Stage 1: spec PR open, not yet approved — correct next step is spec review.
    private static ReleasePlanResponse SpecNotReviewedResponse() => new()
    {
        TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
        PackageType = SdkType.Dataplane,
        Message = "Release plan found",
        ReleasePlanDetails = new ReleasePlanWorkItem
        {
            WorkItemId = 35010,
            Title = "Release Plan - Contoso.WidgetManager (spec review pending)",
            Status = "Active",
            Owner = "testuser@microsoft.com",
            SDKReleaseMonth = "10/2026",
            ReleasePlanId = 50010,
            IsDataPlane = true,
            SpecType = "TypeSpec",
            ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/50010",
            APISpecProjectPath = "specification/contosowidgetmanager/Contoso.WidgetManager",
            SDKReleaseType = "beta",
            IsSpecApproved = false,
            SDKInfo = []
        }
    };

    // Stage 2: spec approved, no SDK work started — correct next step is SDK generation.
    private static ReleasePlanResponse SpecApprovedNoSdkResponse() => new()
    {
        TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
        PackageType = SdkType.Dataplane,
        Message = "Release plan found",
        ReleasePlanDetails = new ReleasePlanWorkItem
        {
            WorkItemId = 35011,
            Title = "Release Plan - Contoso.WidgetManager (ready for SDK generation)",
            Status = "Active",
            Owner = "testuser@microsoft.com",
            SDKReleaseMonth = "10/2026",
            ReleasePlanId = 50011,
            IsDataPlane = true,
            SpecType = "TypeSpec",
            ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/50011",
            APISpecProjectPath = "specification/contosowidgetmanager/Contoso.WidgetManager",
            SDKReleaseType = "beta",
            IsSpecApproved = true,
            SDKInfo = []
        }
    };

    // Stage 3: SDK generated, PRs open but not merged/released — correct next step is review/release.
    private static ReleasePlanResponse SdkGeneratedPendingReleaseResponse() => new()
    {
        TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
        PackageType = SdkType.Dataplane,
        Message = "Release plan found",
        ReleasePlanDetails = new ReleasePlanWorkItem
        {
            WorkItemId = 35012,
            Title = "Release Plan - Contoso.WidgetManager (SDK PRs pending release)",
            Status = "Active",
            Owner = "testuser@microsoft.com",
            SDKReleaseMonth = "10/2026",
            ReleasePlanId = 50012,
            IsDataPlane = true,
            SpecType = "TypeSpec",
            ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/50012",
            APISpecProjectPath = "specification/contosowidgetmanager/Contoso.WidgetManager",
            SDKReleaseType = "beta",
            IsSpecApproved = true,
            SDKInfo =
            [
                new SDKInfo { Language = "Python", PackageName = "azure-contoso-widgetmanager", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/50012", GenerationStatus = "Completed", ReleaseStatus = "NotReleased" },
                new SDKInfo { Language = "JavaScript", PackageName = "@azure/contoso-widgetmanager", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-js/pull/50013", GenerationStatus = "Completed", ReleaseStatus = "NotReleased" },
            ]
        }
    };

    // Stage 4: SDKs released — correct next step is confirming there's nothing left to do.
    private static ReleasePlanResponse FullyReleasedResponse() => new()
    {
        TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
        PackageType = SdkType.Dataplane,
        Message = "Release plan found",
        ReleasePlanDetails = new ReleasePlanWorkItem
        {
            WorkItemId = 35013,
            Title = "Release Plan - Contoso.WidgetManager (released)",
            Status = "Closed",
            Owner = "testuser@microsoft.com",
            SDKReleaseMonth = "08/2026",
            ReleasePlanId = 50013,
            IsDataPlane = true,
            SpecType = "TypeSpec",
            ActiveSpecPullRequest = "https://github.com/Azure/azure-rest-api-specs/pull/50014",
            APISpecProjectPath = "specification/contosowidgetmanager/Contoso.WidgetManager",
            SDKReleaseType = "beta",
            IsSpecApproved = true,
            SDKInfo =
            [
                new SDKInfo { Language = "Python", PackageName = "azure-contoso-widgetmanager", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/50014", GenerationStatus = "Completed", ReleaseStatus = "Released" },
                new SDKInfo { Language = "JavaScript", PackageName = "@azure/contoso-widgetmanager", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-js/pull/50015", GenerationStatus = "Completed", ReleaseStatus = "Released" },
            ]
        }
    };
}
