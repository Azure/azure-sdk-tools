// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_link_namespace_approval_issue.
/// Switches on workItem ID — returns a linked response for "35000", default otherwise.
/// </summary>
public class LinkNamespaceApprovalHandler : IMockToolHandler
{
    public string ToolName => "azsdk_link_namespace_approval_issue";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var workItemId = arguments?.GetValueOrDefault("releasePlanWorkItemId")?.ToString() ?? "0";
        var issueUrl = arguments?.GetValueOrDefault("namespaceApprovalIssue")?.ToString() ?? "";

        return workItemId switch
        {
            "35000" => LinkedApprovalResponse(workItemId, issueUrl),
            _ => MockToolFactory.GetDefaultResponse()
        };
    }

    private static DefaultCommandResponse LinkedApprovalResponse(string workItemId, string issueUrl) => new()
    {
        Message = $"Namespace approval issue linked to release plan work item {workItemId}",
        Result = new { issueUrl, workItemId }
    };
}
