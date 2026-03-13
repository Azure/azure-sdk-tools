// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// Validates that the agent invokes azsdk_typespec_check_project_in_public_repo followed by
/// azsdk_run_typespec_validation when asked to confirm the project is public and then validate.
/// Migrated from evaluation scenario: Evaluate_CheckPublicRepoThenValidate.
/// </summary>
public class CheckPublicRepoThenValidateScenario : BenchmarkScenario
{
    /// <inheritdoc />
    public override string Name => "check-public-repo-then-validate";

    /// <inheritdoc />
    public override string Description =>
        "Verify the agent calls check public repo then runs TypeSpec validation in order.";

    /// <inheritdoc />
    public override string[] Tags => ["tool-invocation", "azure-rest-api-specs"];

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
        Confirm the TypeSpec project is in the public repo, then run TypeSpec validation.
        Project path: specification/contosowidgetmanager/Contoso.WidgetManager.
        My setup has already been verified, do not run azsdk_verify_setup.
        """;

    /// <inheritdoc />
    public override IEnumerable<IValidator> Validators =>
    [
        new ToolCallValidator(
            "Expected tools: check public repo and validate",
            expectedToolCalls:
            [
                new ExpectedToolCall("azsdk_typespec_check_project_in_public_repo",
                    new Dictionary<string, object?>
                    {
                        ["typeSpecProjectPath"] = "specification/contosowidgetmanager/Contoso.WidgetManager"
                    }),
                new ExpectedToolCall("azsdk_run_typespec_validation",
                    new Dictionary<string, object?>
                    {
                        ["typeSpecProjectRootPath"] = "specification/contosowidgetmanager/Contoso.WidgetManager"
                    })
            ],
            forbiddenToolNames: ["azsdk_verify_setup"],
            enforceOrder: false)
    ];
}
