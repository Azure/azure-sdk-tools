// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Mock.Handlers.Package;

/// <summary>
/// Mock handler for azsdk_release_sdk.
/// Switches on packageName — returns release readiness or pipeline info for known packages, default otherwise.
/// </summary>
public class ReleaseSdkHandler : IMockToolHandler
{
    public string ToolName => "azsdk_release_sdk";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var packageName = arguments?.GetValueOrDefault("packageName")?.ToString() ?? "";
        var language = arguments?.GetValueOrDefault("language")?.ToString() ?? ".NET";
        var checkReady = arguments?.GetValueOrDefault("checkReady")?.ToString()
            ?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        return packageName.ToLowerInvariant() switch
        {
            "azure.template.contoso"
                => checkReady ? ReadyToReleaseResponse(packageName, language) : QueuedReleaseResponse(packageName, language),
            _ => MockToolFactory.GetDefaultResponse()
        };
    }

    private static SdkReleaseResponse ReadyToReleaseResponse(string packageName, string language)
    {
        var response = new SdkReleaseResponse
        {
            PackageName = packageName,
            Version = "1.0.0-beta.1",
            PackageType = SdkType.Dataplane,
            TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
            ReleasePipelineStatus = "Ready",
            ReleaseStatusDetails = $"Package {packageName} is ready for release"
        };
        response.SetLanguage(language);
        return response;
    }

    private static SdkReleaseResponse QueuedReleaseResponse(string packageName, string language)
    {
        var response = new SdkReleaseResponse
        {
            PackageName = packageName,
            Version = "1.0.0-beta.1",
            PackageType = SdkType.Dataplane,
            TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
            PipelineBuildId = 80001,
            ReleasePipelineRunUrl = "https://dev.azure.com/azure-sdk/internal/_build/results?buildId=80001",
            ReleasePipelineStatus = "Queued",
            ReleaseStatusDetails = $"Release pipeline triggered for {packageName}. Approve the release in the pipeline to publish to public registries."
        };
        response.SetLanguage(language);
        return response;
    }
}
