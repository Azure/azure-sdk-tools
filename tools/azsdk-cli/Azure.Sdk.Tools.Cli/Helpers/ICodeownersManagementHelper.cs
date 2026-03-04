// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers;

public interface ICodeownersManagementHelper
{
    Task<CodeownersViewResponse> GetViewByUser(string alias, string? repo);
    Task<CodeownersViewResponse> GetViewByLabel(string[] labels, string? repo);
    Task<CodeownersViewResponse> GetViewByPath(string path, string? repo);
    Task<CodeownersViewResponse> GetViewByPackage(string packageName, string? repo = null);

    // Find-or-create helpers
    Task<OwnerWorkItem> FindOrCreateOwnerAsync(string gitHubAlias);
    Task<LabelOwnerWorkItem> FindOrCreateLabelOwnerAsync(string repo, string ownerType, string? repoPath, string label);

    // Add scenarios
    Task<CodeownersModifyResponse> AddOwnerToPackageAsync(string ownerAlias, string packageName, string repo);
    Task<CodeownersModifyResponse> AddLabelToPackageAsync(string label, string packageName, string repo);
    Task<CodeownersModifyResponse> AddOwnerToLabelAsync(string ownerAlias, string label, string repo, string ownerType);
    Task<CodeownersModifyResponse> AddOwnerAndLabelToPathAsync(string ownerAlias, string label, string repo, string path, string ownerType);

    // Remove scenarios
    Task<CodeownersModifyResponse> RemoveOwnerFromPackageAsync(string ownerAlias, string packageName, string repo);
    Task<CodeownersModifyResponse> RemoveLabelFromPackageAsync(string label, string packageName, string repo);
    Task<CodeownersModifyResponse> RemoveOwnerFromLabelAsync(string ownerAlias, string label, string repo, string ownerType);
    Task<CodeownersModifyResponse> RemoveOwnerAndLabelFromPathAsync(string ownerAlias, string label, string repo, string path, string ownerType);
}
