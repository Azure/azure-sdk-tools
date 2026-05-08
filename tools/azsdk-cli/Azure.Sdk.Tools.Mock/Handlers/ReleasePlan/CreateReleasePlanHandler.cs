// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_create_release_plan.
/// Switches on typespec project path — returns a Contoso release plan for the expected path, default otherwise.
/// </summary>
public class CreateReleasePlanHandler : IMockToolHandler
{
    public string ToolName => "azsdk_create_release_plan";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var typespecPath = arguments?.GetValueOrDefault("typeSpecProjectPath")?.ToString() ?? "";

        return typespecPath.ToLowerInvariant() switch
        {
            "specification/contosowidgetmanager/contoso.widgetmanager" => ContosoReleasePlanResponse(typespecPath, arguments),
            _ => MockToolFactory.GetDefaultResponse()
        };
    }

    private static ReleasePlanResponse ContosoReleasePlanResponse(string typespecPath, Dictionary<string, object?>? arguments) => new()
    {
        TypeSpecProject = typespecPath,
        PackageType = SdkType.Dataplane,
        Message = "Release plan created successfully",
        ReleasePlanDetails = new ReleasePlanWorkItem
        {
            WorkItemId = 35000,
            Title = "Release Plan - Contoso.WidgetManager",
            Status = "Active",
            Owner = "testuser@microsoft.com",
            SDKReleaseMonth = arguments?.GetValueOrDefault("targetReleaseMonthYear")?.ToString() ?? "06/2026",
            ReleasePlanId = 50001,
            ReleasePlanLink = "https://dev.azure.com/azure-sdk/Release/_workitems/edit/35000",
            IsDataPlane = true,
            SpecType = "TypeSpec",
            ActiveSpecPullRequest = arguments?.GetValueOrDefault("specPullRequestUrl")?.ToString()
                ?? "https://github.com/Azure/azure-rest-api-specs/pull/12345",
            APISpecProjectPath = typespecPath,
            SDKReleaseType = "beta",
            SDKInfo =
            [
                new SDKInfo { Language = ".NET", PackageName = "Azure.Template.Contoso" },
                new SDKInfo { Language = "Python", PackageName = "azure-contoso-widgetmanager" },
                new SDKInfo { Language = "JavaScript", PackageName = "@azure/contoso-widgetmanager" },
                new SDKInfo { Language = "Java", PackageName = "azure-contoso-widgetmanager" },
            ]
        }
    };
}
