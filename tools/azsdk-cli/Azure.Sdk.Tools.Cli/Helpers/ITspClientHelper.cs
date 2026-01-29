// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Abstraction for running common tsp-client commands (convert, update, generate, init).
/// This centralizes npm exec invocation logic so multiple tools (Generate, Build, Update workflows)
/// can share a single implementation without duplicating code.
/// </summary>
public interface ITspClientHelper
{
    /// <summary>
    /// Runs `tsp-client convert --swagger-readme <readme> --output-dir <out>` with optional flags.
    /// </summary>
    /// <param name="swaggerReadmePath">Path to the swagger readme file.</param>
    /// <param name="outputDirectory">Directory to place the converted output.</param>
    /// <param name="isArm">Whether this is an ARM (management plane) spec.</param>
    /// <param name="fullyCompatible">Whether to use fully compatible mode.</param>
    /// <param name="isCli">True when invoked from CLI flow (suppresses duplicate streamed output in error text).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<TspToolResponse> ConvertSwaggerAsync(string swaggerReadmePath, string outputDirectory, bool isArm, bool fullyCompatible, bool isCli, CancellationToken ct = default);

    /// <summary>
    /// Runs `tsp-client update` to regenerate a TypeSpec client using the tsp-location.yaml in the package directory.
    /// </summary>
    /// <param name="tspLocationDirectory">Path to the directory containing tsp-location.yaml.</param>
    /// <param name="commitSha">Optional commit SHA to update the tsp-location.yaml with before regeneration. If null, uses the commit SHA from the existing tsp-location.yaml.</param>
    /// <param name="isCli">True when invoked from CLI flow (suppresses duplicate streamed output in error text).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<TspToolResponse> UpdateGenerationAsync(string tspLocationDirectory, string? commitSha = null, bool isCli = false, CancellationToken ct = default);

    /// <summary>
    /// Runs `tsp-client init` to initialize SDK generation from a tspconfig.yaml file.
    /// </summary>
    /// <param name="workingDirectory">Working directory where the SDK will be generated (typically the SDK repo root).</param>
    /// <param name="tspConfigPath">Path to the tspconfig.yaml file (can be local path or remote HTTPS URL).</param>
    /// <param name="additionalArgs">Optional additional arguments to pass to tsp-client init (e.g., ["--repo", "Azure/azure-rest-api-specs", "--emitter-options", "key=value"]).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<TspToolResponse> InitializeGenerationAsync(string workingDirectory, string tspConfigPath, string[]? additionalArgs = null, CancellationToken ct = default);
}
