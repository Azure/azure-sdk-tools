// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Tools.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_run_generate_sdk.
/// Returns deterministic generation results for the known release-plan fixtures.
/// </summary>
public class RunGenerateSdkHandler : IMockToolHandler
{
    public string ToolName => "azsdk_run_generate_sdk";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var workItemId = arguments?.GetValueOrDefault("workItemId")?.ToString() ?? "0";
        var language = new InputSanitizer().SanitizeLanguage(arguments?.GetValueOrDefault("language")?.ToString() ?? "");
        var typeSpecProjectRoot = arguments?.GetValueOrDefault("typespecProjectRoot")?.ToString() ?? "";
        var sdkReleaseType = arguments?.GetValueOrDefault("sdkReleaseType")?.ToString() ?? "";
        var apiVersion = arguments?.GetValueOrDefault("apiVersion")?.ToString() ?? "";
        var response = new ReleaseWorkflowResponse { Status = "Failed", ResponseErrors = [] };
        response.SetLanguage(language);

        if (workItemId is not ("35000" or "50001" or "35001" or "50002"))
        {
            response.ResponseErrors.Add("A known release plan work item ID or release plan ID is required to run SDK generation.");
            return response;
        }

        // Like the production tool, private preview exits before generation validation or queueing.
        if (workItemId is "35001" or "50002")
        {
            response.Status = "Success";
            response.Details.Add("Private Preview: SDK cannot be generated and released. Only merge the API spec PR; if SDK validation is required, generate locally for validation only.");
            return response;
        }

        if (!SpecWorkflowTool.IsValidLanguage(language))
        {
            response.ResponseErrors.Add($"SDK generation is not supported for language '{language}'.");
        }
        if (!string.Equals(typeSpecProjectRoot, "specification/contosowidgetmanager/Contoso.WidgetManager", StringComparison.OrdinalIgnoreCase))
        {
            response.ResponseErrors.Add($"Unknown fixture TypeSpec project root path [{typeSpecProjectRoot}].");
        }
        if (!string.Equals(sdkReleaseType, "beta", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sdkReleaseType, "stable", StringComparison.OrdinalIgnoreCase))
        {
            response.ResponseErrors.Add("SDK release type must be set as either beta or stable to generate SDK.");
        }
        if (string.Equals(sdkReleaseType, "stable", StringComparison.OrdinalIgnoreCase)
            && apiVersion.Contains("-preview", StringComparison.OrdinalIgnoreCase))
        {
            response.ResponseErrors.Add($"Stable SDK generation is not allowed from preview API version '{apiVersion}'.");
        }
        if (response.ResponseErrors.Count > 0)
        {
            return response;
        }

        response.Status = "Queued";
        response.TypeSpecProject = typeSpecProjectRoot;
        response.Details =
        [
            "SDK generation pipeline triggered",
            "Pipeline build ID: 90001",
            "Monitor status using azsdk_get_pipeline_status"
        ];
        return response;
    }
}
