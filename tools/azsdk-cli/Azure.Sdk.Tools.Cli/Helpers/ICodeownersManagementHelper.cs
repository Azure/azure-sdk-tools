// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Business logic layer for CODEOWNERS management operations.
/// </summary>
public interface ICodeownersManagementHelper
{
    // View methods
    Task<CodeownersViewResult> GetViewByUserAsync(string alias, string? repo);
    Task<CodeownersViewResult> GetViewByLabelAsync(string label, string? repo);
    Task<CodeownersViewResult> GetViewByPathAsync(string path, string? repo);
    Task<CodeownersViewResult> GetViewByPackageAsync(string packageName);

    // Add methods
    Task<string> AddOwnerToPackageAsync(string alias, string packageName, string repo);
    Task<string> AddOwnerToLabelAsync(string alias, List<string> labels, string repo, string ownerType, string? path);
    Task<string> AddOwnerToPathAsync(string alias, string repo, string path, string ownerType);
    Task<string> AddLabelToPathAsync(List<string> labels, string repo, string path);

    // Remove methods
    Task<string> RemoveOwnerFromPackageAsync(string alias, string packageName, string repo);
    Task<string> RemoveOwnerFromLabelAsync(string alias, List<string> labels, string repo, string ownerType);
    Task<string> RemoveOwnerFromPathAsync(string alias, string repo, string path, string ownerType);
    Task<string> RemoveLabelFromPathAsync(List<string> labels, string repo, string path);
}
