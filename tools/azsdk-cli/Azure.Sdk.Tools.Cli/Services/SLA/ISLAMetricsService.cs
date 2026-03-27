// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models.Responses.SLA;

namespace Azure.Sdk.Tools.Cli.Services.SLA;

public interface ISLAMetricsService
{
    /// <summary>
    /// Computes SLA status for a given service label across one or more repos.
    /// </summary>
    Task<SLAStatusResponse> ComputeSLAStatusAsync(
        string serviceLabel,
        string? repo,
        int lookbackDays,
        int approachingWindowDays,
        bool includeClosed,
        CancellationToken ct);
}
