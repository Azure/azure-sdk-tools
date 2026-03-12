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
    Task<LabelOwnerWorkItem> FindOrCreateLabelOwnerAsync(
        string repo,
        OwnerType ownerType,
        string? repoPath,
        LabelWorkItem[] labelWorkItems
    );

    Task<OwnerWorkItem?> FindOwnerByGitHubAlias(string alias);
    Task<LabelWorkItem?> FindLabelByName(string labelName);

    // Add scenarios
    Task<CodeownersModifyResponse> AddOwnersToPackage(OwnerWorkItem[] owners, string packageName, string repo);
    Task<CodeownersModifyResponse> AddLabelsToPackage(LabelWorkItem[] labels, string packageName, string repo);
    Task<CodeownersModifyResponse> AddOwnersAndLabelsToPath(OwnerWorkItem[] owners, LabelWorkItem[] labels, string repo, string path, OwnerType ownerType);

    // Remove scenarios
    Task<CodeownersModifyResponse> RemoveOwnersFromPackage(OwnerWorkItem[] owners, string packageName, string repo);
    Task<CodeownersModifyResponse> RemoveLabelsFromPackage(LabelWorkItem[] labels, string packageName, string repo);
    Task<CodeownersModifyResponse> RemoveOwnersFromLabelsAndPath(OwnerWorkItem[] owners, LabelWorkItem[] labels, string repo, string path, OwnerType ownerType);

    // Release gate
    Task<ReleaseGateResult> CheckReleaseGateAsync(string packageName, string repo, string packageDirectory, CancellationToken ct = default);
}
