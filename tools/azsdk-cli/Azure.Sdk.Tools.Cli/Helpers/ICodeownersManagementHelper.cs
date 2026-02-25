// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Business logic layer for CODEOWNERS management operations.
/// </summary>
public interface ICodeownersManagementHelper
{
    Task<CodeownersViewResult> GetViewByUser(string alias, string? repo);
    Task<CodeownersViewResult> GetViewByLabel(List<string> labels, string? repo);
    Task<CodeownersViewResult> GetViewByPath(string path, string? repo);
    Task<CodeownersViewResult> GetViewByPackage(string packageName);
}
