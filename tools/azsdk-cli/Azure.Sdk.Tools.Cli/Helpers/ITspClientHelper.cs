// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models.Responses;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Abstraction for running common tsp-client commands (convert, update, generate, init).
/// This centralizes npx invocation logic so multiple tools (Generate, Build, Update workflows)
/// can share a single implementation without duplicating code.
/// </summary>
public interface ITspClientHelper
{
    /// <summary>
    /// Runs `tsp-client convert --swagger-readme <readme> --output-dir <out>` with optional flags.
    /// </summary>
    Task<TspToolResponse> ConvertSwaggerAsync(string swaggerReadmePath, string outputDirectory, bool isArm, bool fullyCompatible, bool isCli, CancellationToken ct);

    /// <summary>
    /// Runs `tsp-client update` to regenerate a TypeSpec client into the specified output directory.
    /// </summary>
    /// <param name="tspLocationPath">Path to the tsp-location.yaml file.</param>
    /// <param name="outputDirectory">Directory to place regenerated output (created if missing, must be empty or created new).</param>
    /// <param name="isCli">True when invoked from CLI flow (suppresses duplicate streamed output in error text).</param>
    Task<TspToolResponse> UpdateGenerationAsync(string tspLocationPath, string outputDirectory, bool isCli, CancellationToken ct);
}
