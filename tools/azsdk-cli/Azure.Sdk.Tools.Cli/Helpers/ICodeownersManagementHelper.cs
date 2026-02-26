// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers;

public interface ICodeownersManagementHelper
{
    Task<CodeownersViewResult> GetViewByUser(string alias, string? repo);
    Task<CodeownersViewResult> GetViewByLabel(string[] labels, string? repo);
    Task<CodeownersViewResult> GetViewByPath(string path, string? repo);
    Task<CodeownersViewResult> GetViewByPackage(string packageName);
}
