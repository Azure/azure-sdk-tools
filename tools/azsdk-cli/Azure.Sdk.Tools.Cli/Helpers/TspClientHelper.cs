// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <inheritdoc />
public class TspClientHelper : ITspClientHelper
{
    private readonly INpxHelper npxHelper;
    private readonly ILogger<TspClientHelper> logger;
    private const int CommandTimeoutInMinutes = 30;

    public TspClientHelper(INpxHelper npxHelper, ILogger<TspClientHelper> logger)
    {
        this.npxHelper = npxHelper;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<TspToolResponse> ConvertSwaggerAsync(string swaggerReadmePath, string outputDirectory, bool isArm, bool fullyCompatible, bool isCli, CancellationToken ct)
    {
        logger.LogInformation("tsp-client convert: {readme} -> {out} (arm={arm}, fullyCompatible={fc})", swaggerReadmePath, outputDirectory, isArm, fullyCompatible);
        var npxOptions = new NpxOptions(
            "@azure-tools/typespec-client-generator-cli",
            ["tsp-client", "convert", "--swagger-readme", swaggerReadmePath, "--output-dir", outputDirectory],
            logOutputStream: true
        );

        if (isArm)
        {
            npxOptions.AddArgs("--arm");
        }
        if (fullyCompatible)
        {
            npxOptions.AddArgs("--fully-compatible");
        }

        var result = await npxHelper.Run(npxOptions, ct);
        if (result.ExitCode != 0)
        {
            // Returning failure; omit verbose output in CLI mode since stream already logged.
            return new TspToolResponse
            {
                ResponseError = isCli
                    ? "Failed to convert swagger to TypeSpec project, see details in the above logs."
                    : "Failed to convert swagger to TypeSpec project, see generator output below" + Environment.NewLine + result.Output,
                TypeSpecProject = outputDirectory
            };
        }

        return new TspToolResponse
        {
            IsSuccessful = true,
            TypeSpecProject = outputDirectory
        };
    }

    public async Task<TspToolResponse> UpdateGenerationAsync(string tspLocationPath, string outputDirectory, string? commitSha = null, bool isCli = false, CancellationToken ct = default)
    {
        logger.LogInformation("tsp-client update (tsp-location): {loc} -> {out}, commit: {commit}", tspLocationPath, outputDirectory, commitSha ?? "");
        
        if (!File.Exists(tspLocationPath))
        {
            return new TspToolResponse {
                ResponseError = $"tsp-location.yaml not found at path: {tspLocationPath}",
                TypeSpecProject = outputDirectory
            };
        }
        var workingDir = Path.GetDirectoryName(Path.GetFullPath(tspLocationPath))!;
        
        var args = new List<string> { "tsp-client", "update" };
        if (!string.IsNullOrEmpty(commitSha))
        {
            args.Add("--commit");
            args.Add(commitSha);
        }
        
        var npxOptions = new NpxOptions(
            "@azure-tools/typespec-client-generator-cli",
            args.ToArray(),
            logOutputStream: true,
            workingDirectory: workingDir,
            timeout: TimeSpan.FromMinutes(CommandTimeoutInMinutes)
        );

        var result = await npxHelper.Run(npxOptions, ct);
        if (result.ExitCode != 0)
        {
            return new TspToolResponse
            {
                ResponseError = isCli
                    ? "Failed to regenerate TypeSpec client, see details in the above logs."
                    : "Failed to regenerate TypeSpec client, see generator output below" + Environment.NewLine + result.Output,
                TypeSpecProject = outputDirectory
            };
        }

        return new TspToolResponse
        {
            IsSuccessful = true,
            TypeSpecProject = outputDirectory
        };
    }

    public async Task<TspToolResponse> InitializeGenerationAsync(string workingDirectory, string tspConfigPath, string[]? additionalArgs = null, CancellationToken ct = default)
    {
        logger.LogInformation("tsp-client init: {tspConfig} in {workingDir}", tspConfigPath, workingDirectory);

        // Build arguments list dynamically
        var arguments = new List<string> { "tsp-client", "init", "--update-if-exists", "--tsp-config", tspConfigPath };

        if (additionalArgs != null && additionalArgs.Length > 0)
        {
            arguments.AddRange(additionalArgs);
        }

        var npxOptions = new NpxOptions(
            "@azure-tools/typespec-client-generator-cli",
            arguments.ToArray(),
            logOutputStream: true,
            workingDirectory: workingDirectory,
            timeout: TimeSpan.FromMinutes(CommandTimeoutInMinutes)
        );

        var result = await npxHelper.Run(npxOptions, ct);
        if (result.ExitCode != 0)
        {
            return new TspToolResponse
            {
                ResponseError = "Failed to generate TypeSpec client, see details in the logs." + Environment.NewLine + result.Output,
                TypeSpecProject = workingDirectory
            };
        }

        return new TspToolResponse
        {
            IsSuccessful = true,
            TypeSpecProject = workingDirectory
        };
    }
}
