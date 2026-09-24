// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_get_release_plan.
/// Returns the Contoso release plan for its known IDs, TypeSpec project path, or spec PR.
/// </summary>
public class GetReleasePlanHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_release_plan";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var typeSpecProjectPath = ReleasePlanMockResponses.Argument(arguments, "typeSpecProjectPath");
        var apiReleaseType = ReleasePlanMockResponses.Argument(arguments, "apiReleaseType");
        var apiVersion = ReleasePlanMockResponses.Argument(arguments, "apiVersion");

        if (!string.IsNullOrWhiteSpace(apiVersion) && string.IsNullOrWhiteSpace(typeSpecProjectPath))
        {
            return new ReleasePlanResponse { ResponseError = "TypeSpec project path is required when API version is provided." };
        }

        if (!string.IsNullOrWhiteSpace(apiVersion) && string.IsNullOrWhiteSpace(apiReleaseType))
        {
            return new ReleasePlanResponse { ResponseError = "API release type is required when API version is provided. Allowed values: Private Preview, Public Preview, GA" };
        }

        var plan = ReleasePlanMockResponses.FindPlan(arguments);
        if (plan == null ||
            (!string.IsNullOrWhiteSpace(typeSpecProjectPath) && !ReleasePlanMockResponses.IsContosoProject(typeSpecProjectPath)) ||
            (!string.IsNullOrWhiteSpace(apiVersion) && !string.Equals(apiVersion, plan.SpecAPIVersion, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(apiReleaseType) && !string.Equals(apiReleaseType, plan.ApiReleaseType.ToDisplayLabel(), StringComparison.OrdinalIgnoreCase)))
        {
            return new ReleasePlanResponse { Message = "No active release plan fixture matches the supplied selectors." };
        }
        return ReleasePlanMockResponses.PlanResponse(plan, "Release plan found (mock)");
    }
}
