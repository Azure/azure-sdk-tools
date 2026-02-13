// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers;

public interface ICodeownersManagementHelper
{
    // View methods
    Task<CodeownersViewResult> GetViewByUserAsync(string alias, string? repo);
    Task<CodeownersViewResult> GetViewByLabelAsync(string label, string? repo);
    Task<CodeownersViewResult> GetViewByPathAsync(string path, string? repo);
    Task<CodeownersViewResult> GetViewByPackageAsync(string packageName);

    // Add methods
    Task<CodeownersViewResult> AddOwnerToPackageAsync(string alias, string packageName, string repo);
    Task<CodeownersViewResult> AddOwnerToLabelAsync(string alias, List<string> labels, string repo, string ownerType, string? path);
    Task<CodeownersViewResult> AddOwnerToPathAsync(string alias, string repo, string path, string ownerType);
    Task<CodeownersViewResult> AddLabelToPathAsync(List<string> labels, string repo, string path);

    // Remove methods
    Task<CodeownersViewResult> RemoveOwnerFromPackageAsync(string alias, string packageName, string repo);
    Task<CodeownersViewResult> RemoveOwnerFromLabelAsync(string alias, List<string> labels, string repo, string ownerType, string? path);
    Task<CodeownersViewResult> RemoveOwnerFromPathAsync(string alias, string repo, string path, string ownerType);
    Task<CodeownersViewResult> RemoveLabelFromPathAsync(List<string> labels, string repo, string path);
}
