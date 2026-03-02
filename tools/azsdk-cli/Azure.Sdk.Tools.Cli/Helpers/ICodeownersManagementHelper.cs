// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers;

public interface ICodeownersManagementHelper
{
    Task<CodeownersViewResponse> GetViewByUser(string alias, string? repo);
    Task<CodeownersViewResponse> GetViewByLabel(string[] labels, string? repo);
    Task<CodeownersViewResponse> GetViewByPath(string path, string? repo);
    Task<CodeownersViewResponse> GetViewByPackage(string packageName, string? repo = null);
}
