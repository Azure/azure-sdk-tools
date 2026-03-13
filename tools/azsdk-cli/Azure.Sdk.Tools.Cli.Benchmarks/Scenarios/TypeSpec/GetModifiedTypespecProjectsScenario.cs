// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Infrastructure;
using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// Validates that the agent invokes azsdk_get_modified_typespec_projects
/// when asked to list modified TypeSpec projects.
/// Setup creates a feature branch with a modified tspconfig.yaml so
/// git merge-base can find the divergence point from main.
/// Migrated from evaluation scenario: Evaluate_GetModifiedTypespecProjects.
/// </summary>
public class GetModifiedTypespecProjectsScenario : BenchmarkScenario
{
    /// <inheritdoc />
    public override string Name => "get-modified-typespec-projects";

    /// <inheritdoc />
    public override string Description =>
        "Verify the agent calls azsdk_get_modified_typespec_projects to list changes.";

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
    public override async Task SetupAsync(Workspace workspace)
    {
        // Commit a modification so git merge-base HEAD main finds the divergence point.
        var tspConfigPath = "specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml";
        var content = await workspace.ReadFileAsync(tspConfigPath);
        await workspace.WriteFileAsync(tspConfigPath, content + "\n# benchmark test modification\n");

        await workspace.RunCommandAsync("git", "add", ".");
        await workspace.RunCommandAsync("git", "commit", "-m", "test: modify TypeSpec project for benchmark");
    }

    /// <inheritdoc />
    public override string Prompt => """
        List the TypeSpec projects modified in my current branch compared to main.
        My setup has already been verified, do not run azsdk_verify_setup.
        """;

    /// <inheritdoc />
    public override IEnumerable<IValidator> Validators =>
    [
        new ToolCallValidator(
            "Expected tool: azsdk_get_modified_typespec_projects",
            expectedToolCalls:
            [
                new ExpectedToolCall("azsdk_get_modified_typespec_projects",
                    new Dictionary<string, object?>
                    {
                        ["targetBranch"] = "main"
                    })
            ],
            forbiddenToolNames: ["azsdk_verify_setup"])
    ];
}
