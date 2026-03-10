// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers;

public interface ICodeownersManagementHelper
{
    Task<CodeownersViewResponse> GetViewByUser(string alias, string? repo, CancellationToken ct);
    Task<CodeownersViewResponse> GetViewByLabel(string[] labels, string? repo, CancellationToken ct);
    Task<CodeownersViewResponse> GetViewByPath(string path, string? repo, CancellationToken ct);
    Task<CodeownersViewResponse> GetViewByPackage(string packageName, string? repo = null, CancellationToken ct = default);
}
