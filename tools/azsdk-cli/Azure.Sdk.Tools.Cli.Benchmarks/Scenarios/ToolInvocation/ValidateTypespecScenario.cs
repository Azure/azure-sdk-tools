// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// Validates that the agent invokes azsdk_run_typespec_validation when asked to validate
/// a TypeSpec project. The agent may optionally also call azsdk_verify_setup and/or
/// azsdk_typespec_check_project_in_public_repo.
/// Migrated from evaluation scenario: Evaluate_ValidateTypespec.
/// </summary>
public class ValidateTypespecScenario : BenchmarkScenario
{
    /// <inheritdoc />
    public override string Name => "validate-typespec";

    /// <inheritdoc />
    public override string Description =>
        "Verify the agent calls verify setup and run TypeSpec validation.";

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
        Validate my typespec project. It is already confirmed we are in a public repository.
        The path to my typespec is specification/contosowidgetmanager/Contoso.WidgetManager/main.tsp.
        """;

    /// <inheritdoc />
    public override IEnumerable<IValidator> Validators =>
    [
        new ToolCallValidator(
            "Expected tools: validate typespec",
            expectedToolCalls:
            [
                new ExpectedToolCall("azsdk_run_typespec_validation",
                    new Dictionary<string, object?>
                    {
                        ["typeSpecProjectRootPath"] = "specification/contosowidgetmanager/Contoso.WidgetManager"
                    })
            ],
            optionalToolNames: ["azsdk_typespec_check_project_in_public_repo", "azsdk_verify_setup"])
    ];
}
