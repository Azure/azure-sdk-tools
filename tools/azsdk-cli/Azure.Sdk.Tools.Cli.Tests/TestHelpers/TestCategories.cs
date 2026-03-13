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
    /// Tests that call real external services (GitHub Copilot LLM, Azure OpenAI, etc.)
    /// and require authentication / network access.
    /// </summary>
    public const string Integration = "Integration";

    /// <summary>
    /// Tests that require Go tooling (go CLI) to be installed and at a minimum version.
    /// </summary>
    public const string RequiresGoTooling = "RequiresGoTooling";
}
