// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Benchmarks.Models;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation;
using Azure.Sdk.Tools.Cli.Benchmarks.Validation.Validators;

namespace Azure.Sdk.Tools.Cli.Benchmarks.Scenarios;

/// <summary>
/// Validates that the agent invokes azsdk_get_pipeline_status when asked
/// to check SDK generation pipeline status.
/// Adapted from evaluation scenario: Evaluate_CheckSDKGenerationStatus.
/// The original scenario loaded a mid-conversation JSON trace; this benchmark uses a
/// standalone prompt capturing the same intent.
/// </summary>
public class CheckSdkGenerationStatusScenario : BenchmarkScenario
{
    /// <inheritdoc />
    public override string Name => "check-sdk-generation-status";

    /// <inheritdoc />
    public override string Description =>
        "Verify the agent calls azsdk_get_pipeline_status to check SDK generation.";

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
        Check the SDK generation pipeline status for build ID 5513110.
        My setup has already been verified, do not run azsdk_verify_setup.
        """;

    /// <inheritdoc />
    public override IEnumerable<IValidator> Validators =>
    [
        new ToolCallValidator(
            "Expected tool: azsdk_get_pipeline_status",
            expectedToolCalls:
            [
                new ExpectedToolCall("azsdk_get_pipeline_status",
                    new Dictionary<string, object?>
                    {
                        ["buildId"] = 5513110
                    })
            ],
            forbiddenToolNames: ["azsdk_verify_setup"])
    ];
}
