// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

namespace Azure.Sdk.Tools.Mock.Handlers.Config;

/// <summary>
/// Mock handler for azsdk_check_service_label. Convention-driven: the returned status is
/// derived from the requested label name so a single mock can exercise every branch of the
/// real tool. The real MCP parameter is "serviceLabel".
/// </summary>
public class CheckServiceLabelHandler : IMockToolHandler
{
    public string ToolName => "azsdk_check_service_label";
    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var label = arguments?.GetValueOrDefault("serviceLabel")?.ToString() ?? "Contoso.WidgetManager";
        var normalized = label.Replace(" ", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
        var status = normalized switch
        {
            _ when normalized.Contains("existing") => "Exists",
            _ when normalized.Contains("inreview") => "InReview",
            _ when normalized.Contains("notalabel") => "NotAServiceLabel",
            _ => "DoesNotExist"
        };
        return new ServiceLabelResponse
        {
            Label = label,
            Status = status
        };
    }
}

/// <summary>Mock handler for azsdk_create_service_label.</summary>
public class CreateServiceLabelHandler : IMockToolHandler
{
    public string ToolName => "azsdk_create_service_label";
    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var label = arguments?.GetValueOrDefault("label")?.ToString() ?? "Contoso.WidgetManager";
        return new ServiceLabelResponse
        {
            Label = label,
            Status = "Created",
            PullRequestUrl = $"https://github.com/Azure/azure-sdk-tools/pull/99001"
        };
    }
}

/// <summary>Mock handler for azsdk_engsys_codeowner_view.</summary>
public class CodeownerViewHandler : IMockToolHandler
{
    public string ToolName => "azsdk_engsys_codeowner_view";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new CodeownersViewResponse
    {
        Packages =
        [
            new PackageResponse
            {
                WorkItemId = 70001,
                PackageName = "Azure.Template.Contoso",
                Language = ".NET",
                PackageType = "client",
                Owners =
                [
                    new OwnerResponse { GitHubAlias = "contoso-owner-1" },
                    new OwnerResponse { GitHubAlias = "contoso-owner-2" }
                ],
                Labels = ["Contoso.WidgetManager"]
            }
        ]
    };
}

/// <summary>Mock handler for azsdk_engsys_codeowner_check_package.</summary>
public class CodeownerCheckPackageHandler : IMockToolHandler
{
    public string ToolName => "azsdk_engsys_codeowner_check_package";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new CheckPackageResponse
    {
        DirectoryPath = arguments?.GetValueOrDefault("directoryPath")?.ToString() ?? "sdk/contoso/Azure.Template.Contoso",
        Owners = ["contoso-owner-1", "contoso-owner-2"],
        PRLabels = ["Contoso.WidgetManager"],
        ServiceOwners = ["service-team-lead"],
        ServiceLabels = ["Service Attention", "Contoso.WidgetManager"]
    };
}

internal static class CodeownersModifyMockResponses
{
    public static CodeownersModifyResponse OkWithMessage() => new()
    {
        View = new CodeownersViewResponse
        {
            Packages =
            [
                new PackageResponse
                {
                    WorkItemId = 70001,
                    PackageName = "Azure.Template.Contoso",
                    Language = ".NET",
                    PackageType = "client",
                    Owners = [new OwnerResponse { GitHubAlias = "contoso-owner-1" }],
                    Labels = ["Contoso.WidgetManager"]
                }
            ]
        }
    };
}

/// <summary>Mock handler for azsdk_engsys_codeowner_add_package_owner.</summary>
public class CodeownerAddPackageOwnerHandler : IMockToolHandler
{
    public string ToolName => "azsdk_engsys_codeowner_add_package_owner";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => CodeownersModifyMockResponses.OkWithMessage();
}

/// <summary>Mock handler for azsdk_engsys_codeowner_add_package_label.</summary>
public class CodeownerAddPackageLabelHandler : IMockToolHandler
{
    public string ToolName => "azsdk_engsys_codeowner_add_package_label";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => CodeownersModifyMockResponses.OkWithMessage();
}

/// <summary>Mock handler for azsdk_engsys_codeowner_add_label_owner.</summary>
public class CodeownerAddLabelOwnerHandler : IMockToolHandler
{
    public string ToolName => "azsdk_engsys_codeowner_add_label_owner";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => CodeownersModifyMockResponses.OkWithMessage();
}

/// <summary>Mock handler for azsdk_engsys_codeowner_remove_package_owner.</summary>
public class CodeownerRemovePackageOwnerHandler : IMockToolHandler
{
    public string ToolName => "azsdk_engsys_codeowner_remove_package_owner";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => CodeownersModifyMockResponses.OkWithMessage();
}

/// <summary>Mock handler for azsdk_engsys_codeowner_remove_package_label.</summary>
public class CodeownerRemovePackageLabelHandler : IMockToolHandler
{
    public string ToolName => "azsdk_engsys_codeowner_remove_package_label";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => CodeownersModifyMockResponses.OkWithMessage();
}

/// <summary>Mock handler for azsdk_engsys_codeowner_remove_label_owner.</summary>
public class CodeownerRemoveLabelOwnerHandler : IMockToolHandler
{
    public string ToolName => "azsdk_engsys_codeowner_remove_label_owner";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => CodeownersModifyMockResponses.OkWithMessage();
}

/// <summary>Mock handler for azsdk_engsys_codeowner_update_cache.</summary>
public class CodeownerUpdateCacheHandler : IMockToolHandler
{
    public string ToolName => "azsdk_engsys_codeowner_update_cache";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new DefaultCommandResponse
    {
        Message = "CODEOWNERS cache refreshed (mock)",
        Result = new { packagesRefreshed = 1, labelOwnersRefreshed = 1 }
    };
}
