// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.SetupRequirements;

/// <summary>
/// Service for verifying environment setup and optionally installing missing requirements.
/// </summary>
public interface IVerifySetupService
{
    /// <summary>
    /// Verifies the developer environment for MCP release tool requirements.
    /// </summary>
    Task<VerifySetupResponse> VerifySetup(HashSet<SdkLanguage>? langs = null, string? packagePath = null, List<string>? requirementsToInstall = null, CancellationToken ct = default);
}
