// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.Services.Repair;

public sealed class CustomizedCodeRepairService(
    IGitHelper gitHelper,
    ITypeSpecHelper typeSpecHelper,
    ITspClientHelper tspClientHelper,
    IFeedbackClassifierService classifier,
    IRepairSourceState sourceState,
    IRepairArtifacts artifacts,
    TimeProvider timeProvider,
    ILogger<CustomizedCodeRepairService> logger) : ICustomizedCodeRepairService
{
    public Task<CustomizedCodeUpdateResponse> RunAsync(
        CustomizedCodeRepairRequest request,
        Func<string, CancellationToken, Task<LanguageService>> resolveLanguage,
        CancellationToken ct) =>
        new CustomizedCodeRepairSession(request, gitHelper, typeSpecHelper, tspClientHelper,
            classifier, sourceState, artifacts, timeProvider, logger).RunAsync(resolveLanguage, ct);
}
