// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Interface for generating CODEOWNERS files from Azure DevOps work items.
/// </summary>
public interface ICodeownersGenerateHelper
{
    /// <summary>
    /// Generates the CODEOWNERS file for a repository.
    /// </summary>
    /// <param name="repoRoot">Path to the repository root</param>
    /// <param name="repoName">Repository name in the format Azure/azure-sdk-for-{lang}</param>
    /// <param name="packageTypes">Package types to filter by</param>
    /// <param name="sectionName">Section name in CODEOWNERS file to update</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The generated CODEOWNERS content that was written</returns>
    Task GenerateCodeowners(
        string repoRoot,
        string repoName,
        string[] packageTypes,
        string sectionName,
        CancellationToken ct = default);
}
