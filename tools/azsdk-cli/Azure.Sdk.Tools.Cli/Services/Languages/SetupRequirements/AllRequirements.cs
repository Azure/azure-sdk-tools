// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.SetupRequirements;

/// <summary>
/// Combines all requirements from all categories into a single list.
/// Use ShouldCheck on each requirement to filter based on context.
/// </summary>
public static class AllRequirements
{
    public static IReadOnlyList<Requirement> All => [
        ..CoreRequirements.All,
        ..PythonRequirements.All,
        ..JavaRequirements.All,
        ..DotNetRequirements.All,
        ..GoRequirements.All,
        ..JavaScriptRequirements.All
    ];
}
