// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// Validates that the agent invokes azsdk_typespec_check_project_in_public_repo
/// as part of the TypeSpec generation workflow step 2 (validation).
/// Adapted from evaluation scenario: AzsdkTypeSpecGeneration_Step02_TypespecValidation.
/// The original scenario loaded a mid-conversation JSON trace; this benchmark uses a
/// standalone prompt capturing the same intent.
/// </summary>
public class TypespecGenerationStep02Scenario : BenchmarkScenario
{
    /// <inheritdoc />
    public override string Name => "typespec-generation-step02-validation";

    /// <inheritdoc />
    public override string Description =>
        "Verify the agent checks public repo as part of TypeSpec generation step 2.";

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
        I'm working on the TypeSpec generation workflow. I need to validate my TypeSpec project
        as part of step 2. Please check if my TypeSpec project is in the public repo.
        The project is at specification/contosowidgetmanager/Contoso.WidgetManager.
        My setup has already been verified, do not run azsdk_verify_setup.
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
