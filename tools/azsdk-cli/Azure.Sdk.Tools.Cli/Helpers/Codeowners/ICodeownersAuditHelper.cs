// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Responses.Codeowners;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

public interface ICodeownersAuditHelper
{
    Task<CodeownersAuditResponse> RunAudit(bool fix, bool force, string? repo, CancellationToken ct);
}
