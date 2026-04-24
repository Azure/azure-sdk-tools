// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

public interface ICodeownersManagementHelper
{
    Task<CodeownersViewResponse> GetViewByUser(string alias, string? repo, CancellationToken ct);
    Task<CodeownersViewResponse> GetViewByLabel(string[] labels, string? repo, CancellationToken ct);
    Task<CodeownersViewResponse> GetViewByPath(string path, string? repo, CancellationToken ct);
    Task<CodeownersViewResponse> GetViewByPackage(string packageName, string? repo = null, CancellationToken ct = default);

    // Find-or-create helpers
    Task<LabelOwnerWorkItem> FindOrCreateLabelOwnerAsync(
        string repo,
        OwnerType ownerType,
        string? repoPath,
        LabelWorkItem[] labelWorkItems,
        string section,
        CancellationToken ct
    );

    Task<OwnerWorkItem?> FindOwnerByGitHubAlias(string alias, CancellationToken ct);
    Task<LabelWorkItem?> FindLabelByName(string labelName, CancellationToken ct);

    // Add scenarios
    Task<CodeownersModifyResponse> AddOwnersToPackage(OwnerWorkItem[] owners, string packageName, string repo, CancellationToken ct);
    Task<CodeownersModifyResponse> AddLabelsToPackage(LabelWorkItem[] labels, string packageName, string repo, CancellationToken ct);
    Task<CodeownersModifyResponse> AddOwnersAndLabelsToPath(OwnerWorkItem[] owners, LabelWorkItem[] labels, string repo, string path, OwnerType ownerType, string section, CancellationToken ct);

    // Remove scenarios
    Task<CodeownersModifyResponse> RemoveOwnersFromPackage(OwnerWorkItem[] owners, string packageName, string repo, CancellationToken ct);
    Task<CodeownersModifyResponse> RemoveLabelsFromPackage(LabelWorkItem[] labels, string packageName, string repo, CancellationToken ct);
    Task<CodeownersModifyResponse> RemoveOwnersFromLabelsAndPath(OwnerWorkItem[] owners, LabelWorkItem[] labels, string repo, string path, OwnerType ownerType, string section, CancellationToken ct);

    // Validation
    Task ThrowIfInvalidTeamAlias(string alias, CancellationToken ct);
}
