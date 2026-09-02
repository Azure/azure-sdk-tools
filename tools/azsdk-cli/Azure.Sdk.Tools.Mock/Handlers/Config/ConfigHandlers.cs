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

/// <summary>Mock handler for azsdk_engsys_codeowner_generate.</summary>
public class CodeownerGenerateHandler : IMockToolHandler
{
    public string ToolName => "azsdk_engsys_codeowner_generate";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new DefaultCommandResponse
    {
        Message = "CODEOWNERS regenerated from owners.yaml (mock)",
        Result = new { outputPath = ".github/CODEOWNERS", inSync = true }
    };
}

/// <summary>Mock handler for azsdk_engsys_codeowner_audit.</summary>
public class CodeownerAuditHandler : IMockToolHandler
{
    public string ToolName => "azsdk_engsys_codeowner_audit";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new CodeownersAuditResponse
    {
        RepoRoot = arguments?.GetValueOrDefault("repoRoot")?.ToString() ?? "/repo"
    };
}
