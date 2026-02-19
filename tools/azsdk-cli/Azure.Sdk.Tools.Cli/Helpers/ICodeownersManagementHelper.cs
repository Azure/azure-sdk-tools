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
    Task<CodeownersViewResult> GetViewByUser(string alias, string? repo);
    Task<CodeownersViewResult> GetViewByLabel(List<string> labels, string? repo);
    Task<CodeownersViewResult> GetViewByPath(string path, string? repo);
    Task<CodeownersViewResult> GetViewByPackage(string packageName);

    // Add methods
    Task<string> AddOwnerToPackage(string alias, string packageName, string repo);
    Task<string> AddOwnerToLabel(string alias, List<string> labels, string repo, string ownerType, string? path);
    Task<string> AddOwnerToPath(string alias, string repo, string path, string ownerType);
    Task<string> AddLabelToPath(List<string> labels, string repo, string path);

    // Remove methods
    Task<string> RemoveOwnerFromPackage(string alias, string packageName, string repo);
    Task<string> RemoveOwnerFromLabel(string alias, List<string> labels, string repo, string ownerType);
    Task<string> RemoveOwnerFromPath(string alias, string repo, string path, string ownerType);
    Task<string> RemoveLabelFromPath(List<string> labels, string repo, string path);
}
