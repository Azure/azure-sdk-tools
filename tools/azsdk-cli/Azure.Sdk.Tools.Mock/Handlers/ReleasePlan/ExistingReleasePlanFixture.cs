// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// An existing plan returned by the live tool's project/API-version duplicate check.
/// Kept separate from the new-plan fixture so reuse cannot be mistaken for creation.
/// </summary>
internal static class ExistingReleasePlanFixture
{
    public const string ProjectPath = "specification/contosowidgetmanager/Contoso.ExistingRelease";

    public static ReleasePlanResponse CreateResponse() => new()
    {
        TypeSpecProject = ProjectPath,
        PackageType = SdkType.Dataplane,
        Message = $"An existing release plan (ID: 50002) was found for TypeSpec project '{ProjectPath}' with API version '2026-09-01-preview'. No new release plan was created.",
        NextSteps = ["Review the existing release plan and use it for your SDK release."],
        ReleasePlanDetails = new ReleasePlanWorkItem
        {
            WorkItemId = 35002,
            ReleasePlanId = 50002,
            Title = "Release Plan - Contoso.ExistingRelease",
            Status = "In Progress",
            SDKReleaseMonth = "December 2026",
            ApiReleaseType = ApiReleaseType.PublicPreview,
            SpecAPIVersion = "2026-09-01-preview",
            SpecType = "TypeSpec",
            IsDataPlane = true,
            APISpecProjectPath = ProjectPath,
            SDKReleaseType = "beta"
        }
    };
}