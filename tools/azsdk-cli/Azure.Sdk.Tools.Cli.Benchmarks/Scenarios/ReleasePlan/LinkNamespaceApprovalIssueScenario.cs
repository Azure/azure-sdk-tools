// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// Validates that the agent invokes azsdk_link_namespace_approval_issue
/// when asked to link a namespace approval issue to a release plan.
/// Migrated from evaluation scenario: Evaluate_LinkNamespaceApprovalIssue.
/// </summary>
public class LinkNamespaceApprovalIssueScenario : BenchmarkScenario
{
    /// <inheritdoc />
    public override string Name => "link-namespace-approval-issue";

    /// <inheritdoc />
    public override string Description =>
        "Verify the agent calls azsdk_link_namespace_approval_issue to link an issue to a release plan.";

    /// <inheritdoc />
    public override string[] Tags => ["release-plan"];

    /// <inheritdoc />
    public override RepoConfig Repo => new()
    {
        Owner = "Azure",
        Name = "azure-rest-api-specs",
        Ref = "main",
        SparseCheckoutPaths = ["specification/contosowidgetmanager"]
    };

    /// <inheritdoc />
    public override string Prompt => """
        Link namespace approval issue https://github.com/Azure/azure-sdk/issues/1234 to release plan 12345.
        """;

    /// <inheritdoc />
    public override IEnumerable<IValidator> Validators =>
    [
        new ToolCallValidator(
            "Expected tool: azsdk_link_namespace_approval_issue",
            expectedToolCalls:
            [
                new ExpectedToolCall("azsdk_link_namespace_approval_issue",
                    new Dictionary<string, object?>
                    {
                        ["releasePlanWorkItemId"] = 12345,
                        ["namespaceApprovalIssue"] = "https://github.com/Azure/azure-sdk/issues/1234"
                    })
            ],
            optionalToolNames: ["azsdk_verify_setup"])
    ];
}