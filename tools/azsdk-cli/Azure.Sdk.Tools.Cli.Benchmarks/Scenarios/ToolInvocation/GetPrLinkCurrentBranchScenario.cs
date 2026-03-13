// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// Validates that the agent invokes azsdk_get_pull_request_link_for_current_branch
/// when asked about the status of a spec PR on the current branch.
/// Migrated from evaluation scenario: Evaluate_GetPullRequestLinkForCurrentBranch.
/// </summary>
public class GetPrLinkCurrentBranchScenario : BenchmarkScenario
{
    /// <inheritdoc />
    public override string Name => "get-pr-link-current-branch";

    /// <inheritdoc />
    public override string Description =>
        "Verify the agent calls azsdk_get_pull_request_link_for_current_branch for PR status.";

    /// <inheritdoc />
    public override string[] Tags => ["tool-invocation", "general"];

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
        What's the status of the spec PR in my current branch? Only check the status once.
        My setup has already been verified, do not run azsdk_verify_setup.
        Path to my repository root: C:\azure-rest-api-specs.
        """;

    /// <inheritdoc />
    public override IEnumerable<IValidator> Validators =>
    [
        new ToolCallValidator(
            "Expected tool: azsdk_get_pull_request_link_for_current_branch",
            expectedToolCalls:
            [
                new ExpectedToolCall("azsdk_get_pull_request_link_for_current_branch")
            ],
            forbiddenToolNames: ["azsdk_verify_setup"])
    ];
}
