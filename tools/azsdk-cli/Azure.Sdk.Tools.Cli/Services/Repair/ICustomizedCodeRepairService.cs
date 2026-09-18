// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Services.Repair;

public interface ICustomizedCodeRepairService
{
    Task<CustomizedCodeUpdateResponse> RunAsync(
        CustomizedCodeRepairRequest request,
        Func<string, CancellationToken, Task<LanguageService>> resolveLanguage,
        CancellationToken ct);
}
