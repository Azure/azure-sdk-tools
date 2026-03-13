// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Tests.TestHelpers;

/// <summary>
/// Constants for NUnit test categories used to filter tests.
/// Use with [Category(TestCategories.XYZ)] to mark tests.
/// Filter at the CLI: dotnet test --filter "Category!=Integration"
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
    /// Tests that require a real SDK repository clone and related env vars.
    /// </summary>
    public const string Integration = "Integration";

    /// <summary>
    /// Tests that require Go tooling (go CLI) to be installed and at a minimum version.
    /// </summary>
    public const string RequiresGoTooling = "RequiresGoTooling";
}
