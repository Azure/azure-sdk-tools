// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// Validates that the agent invokes azsdk_typespec_check_project_in_public_repo
/// when asked to check if a TypeSpec project is in the public repo.
/// Migrated from evaluation scenario: Evaluate_CheckPublicRepo.
/// </summary>
public class CheckPublicRepoScenario : BenchmarkScenario
{
    /// <inheritdoc />
    public override string Name => "check-public-repo";

    /// <inheritdoc />
    public override string Description =>
        "Verify the agent calls azsdk_typespec_check_project_in_public_repo.";

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
        Check if my TypeSpec project is in the public repo.
        My setup has already been verified, do not run azsdk_verify_setup.
        Project root: specification/contosowidgetmanager/Contoso.WidgetManager.
        """;

    /// <inheritdoc />
    public override IEnumerable<IValidator> Validators =>
    [
        new ToolCallValidator(
            "Expected tool: azsdk_typespec_check_project_in_public_repo",
            expectedToolCalls:
            [
                new ExpectedToolCall("azsdk_typespec_check_project_in_public_repo",
                    new Dictionary<string, object?>
                    {
                        ["typeSpecProjectPath"] = "specification/contosowidgetmanager/Contoso.WidgetManager"
                    })
            ],
            forbiddenToolNames: ["azsdk_verify_setup"])
    ];
}
