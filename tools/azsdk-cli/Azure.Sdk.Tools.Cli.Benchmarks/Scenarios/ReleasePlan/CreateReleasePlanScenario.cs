// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// Validates that the agent invokes the azsdk_create_release_plan tool when asked to create a release plan.
/// Migrated from evaluation scenario: Evaluate_CreateReleasePlan.
/// </summary>
public class CreateReleasePlanScenario : BenchmarkScenario
{
    /// <inheritdoc />
    public override string Name => "create-release-plan";

    /// <inheritdoc />
    public override string Description =>
        "Verify the agent calls azsdk_create_release_plan with appropriate context.";

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
        Create a release plan for the Contoso Widget Manager, no need to get it afterwards only create.
        My setup has already been verified, do not run azsdk_verify_setup. Here is all the context you need:
        TypeSpec project located at "specification/contosowidgetmanager/Contoso.WidgetManager".
        Use service tree ID "a7f2b8e4-9c1d-4a3e-b6f9-2d8e5a7c3b1f",
        product tree ID "f1a8c5d2-6e4b-4f7a-9c2d-8b5e1f3a6c9e",
        target release timeline "December 2025",
        API version "2022-11-01-preview",
        SDK release type "beta",
        and link it to the spec pull request "https://github.com/Azure/azure-rest-api-specs/pull/38387".
        """;

    /// <inheritdoc />
    public override IEnumerable<IValidator> Validators =>
    [
        new ToolCallValidator(
            "Expected tool: azsdk_create_release_plan",
            expectedToolCalls:
            [
                new ExpectedToolCall("azsdk_create_release_plan",
                    new Dictionary<string, object?>
                    {
                        ["typeSpecProjectPath"] = "specification/contosowidgetmanager/Contoso.WidgetManager",
                        ["serviceTreeId"] = "a7f2b8e4-9c1d-4a3e-b6f9-2d8e5a7c3b1f",
                        ["productTreeId"] = "f1a8c5d2-6e4b-4f7a-9c2d-8b5e1f3a6c9e",
                        ["specApiVersion"] = "2022-11-01-preview",
                        ["specPullRequestUrl"] = "https://github.com/Azure/azure-rest-api-specs/pull/38387",
                        ["sdkReleaseType"] = "beta"
                    })
            ],
            forbiddenToolNames: ["azsdk_verify_setup"])
    ];
}
