// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Abstraction for running common tsp-client commands (convert, update, diff, map, etc.).
/// This centralizes npx invocation logic so multiple tools (Generate, Build, Update workflows)
/// can share a single implementation without instantiating each other.
/// </summary>
public interface ITspClientHelper
{
    /// <summary>
    /// Runs `tsp-client convert --swagger-readme <readme> --output-dir <out>` with optional flags.
    /// </summary>
    Task<TspToolResponse> ConvertSwaggerAsync(string swaggerReadmePath, string outputDirectory, bool isArm, bool fullyCompatible, bool isCli, CancellationToken ct);
}
