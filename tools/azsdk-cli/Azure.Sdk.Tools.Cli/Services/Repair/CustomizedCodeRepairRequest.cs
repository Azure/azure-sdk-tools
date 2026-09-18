// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.Repair;

public sealed record CustomizedCodeRepairRequest(
    string? PackagePath,
    string? TypeSpecProjectPath,
    string CustomizationRequest,
    EditScope EditScope,
    int MaxAttempts = 1,
    int TimeoutMinutes = 30,
    string? ArtifactsPath = null)
{
    public string? ApiViewUrl { get; init; }
}
