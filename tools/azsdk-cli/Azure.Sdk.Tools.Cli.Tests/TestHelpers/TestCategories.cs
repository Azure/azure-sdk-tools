// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Tests.TestHelpers;

/// <summary>
/// Constants for NUnit test categories used to filter tests.
/// Use with [Category(TestCategories.XYZ)] to mark tests.
/// Filter at the CLI: dotnet test --filter "Category==Evals"
/// </summary>
public static class TestCategories
{
    /// <summary>
    /// Tests that call real GitHub Copilot Agent / LLM endpoints
    /// and require the Copilot CLI to be installed and authenticated.
    /// </summary>
    public const string CopilotAgent = "CopilotAgent";

    /// <summary>
    /// Tests that call real Azure OpenAI endpoints
    /// and require AZURE_OPENAI_ENDPOINT (and related env vars) to be set.
    /// </summary>
    public const string OpenAI = "OpenAI";

    /// <summary>
    /// Tests that require real/mock data upstream in devops work items
    /// </summary>
    public const string ReleasePlan = "ReleasePlan";

    /// <summary>
    /// Tests that require Go tooling (go CLI) to be installed and at a minimum version.
    /// </summary>
    public const string RequiresGoTooling = "RequiresGoTooling";

    /// <summary>
    /// Evaluation tests that require Azure OpenAI, MCP server, and related env vars.
    /// Used to categorize evaluation scenarios in the Evaluations project.
    /// </summary>
    public const string Evals = "Evals";
}
