// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models.Codeowners;

/// <summary>
/// Represents a package from the repository returned by Get-AllPkgProperties.ps1
/// </summary>
public record RepoPackage
{
    public string Name { get; init; } = "";
    public string DirectoryPath { get; init; } = "";
    public string ServiceDirectory { get; init; } = "";
}
