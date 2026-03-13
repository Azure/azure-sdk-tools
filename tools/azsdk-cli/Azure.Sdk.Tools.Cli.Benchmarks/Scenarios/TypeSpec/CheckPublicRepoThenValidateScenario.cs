// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// Validates that the agent invokes azsdk_run_typespec_validation followed by
/// azsdk_typespec_check_project_in_public_repo when asked to validate and then check public repo.
/// Migrated from evaluation scenario: Evaluate_CheckPublicRepoThenValidate.
/// </summary>
public class CheckPublicRepoThenValidateScenario : BenchmarkScenario
{
    /// <inheritdoc />
    public override string Name => "validate-then-check-public-repo";

    /// <inheritdoc />
    public override string Description =>
        "Verify the agent runs TypeSpec validation then checks public repo.";

    /// <inheritdoc />
    public override string[] Tags => ["typespec"];

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
        Run TypeSpec validation, then check if the project is in the public repo.
        Project path: specification/contosowidgetmanager/Contoso.WidgetManager.
        My setup has already been verified, do not run azsdk_verify_setup.
        """;

    /// <inheritdoc />
    public override IEnumerable<IValidator> Validators =>
    [
        new ToolCallValidator(
            "Expected tools: validate then check public repo",
            expectedToolCalls:
            [
                new ExpectedToolCall("azsdk_run_typespec_validation",
                    new Dictionary<string, object?>
                    {
                        ["typeSpecProjectRootPath"] = "specification/contosowidgetmanager/Contoso.WidgetManager"
                    }),
                new ExpectedToolCall("azsdk_typespec_check_project_in_public_repo",
                    new Dictionary<string, object?>
                    {
                        ["typeSpecProjectPath"] = "specification/contosowidgetmanager/Contoso.WidgetManager"
                    })
            ],
            forbiddenToolNames: ["azsdk_verify_setup"])
    ];
}
