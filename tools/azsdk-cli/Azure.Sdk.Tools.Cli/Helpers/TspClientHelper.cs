// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models.Responses.TypeSpec;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <inheritdoc />
public class TspClientHelper : ITspClientHelper
{
    private readonly INpmHelper npmHelper;
    private readonly ITypeSpecHelper typeSpecHelper;
    private readonly IGitHelper gitHelper;
    private readonly ILogger<TspClientHelper> logger;
    private const int CommandTimeoutInMinutes = 30;

    public TspClientHelper(INpmHelper npmHelper, ITypeSpecHelper typeSpecHelper, IGitHelper gitHelper, ILogger<TspClientHelper> logger)
    {
        this.npmHelper = npmHelper;
        this.typeSpecHelper = typeSpecHelper;
        this.gitHelper = gitHelper;
        this.logger = logger;
    }

    /// <summary>
    /// Gets the npm prefix path based on the repository type.
    /// For azure-rest-api-specs (public or private), uses the repo root folder directly.
    /// For SDK repos, uses eng/common/tsp-client under the repo root.
    /// </summary>
    private async Task<string> GetNpmPrefixAsync(string repoRootFolder, CancellationToken ct)
    {
        var isSpecRepo = await typeSpecHelper.IsRepoPathForSpecRepoAsync(repoRootFolder, ct);
        if (isSpecRepo)
        {
            return repoRootFolder;
        }
        return Path.Combine(repoRootFolder, "eng", "common", "tsp-client");
    }

    /// <inheritdoc />
    public async Task<TspToolResponse> ConvertSwaggerAsync(string swaggerReadmePath, string outputDirectory, bool isArm, bool fullyCompatible, bool isCli, CancellationToken ct = default)
    {
        logger.LogInformation("tsp-client convert: {readme} -> {out} (arm={arm}, fullyCompatible={fc})", swaggerReadmePath, outputDirectory, isArm, fullyCompatible);
        var repoRootFolder = await gitHelper.DiscoverRepoRootAsync(swaggerReadmePath, ct);
        var npmPrefix = await GetNpmPrefixAsync(repoRootFolder, ct);
        var npmOptions = new NpmOptions(
            npmPrefix,
            ["tsp-client", "convert", "--swagger-readme", swaggerReadmePath, "--output-dir", outputDirectory],
            logOutputStream: true
        );

        if (isArm)
        {
            npmOptions.AddArgs("--arm");
        }
        if (fullyCompatible)
        {
            npmOptions.AddArgs("--fully-compatible");
        }

        var result = await npmHelper.Run(npmOptions, ct);
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

    /// <inheritdoc />
    public async Task<TspToolResponse> UpdateGenerationAsync(string packagePath, string? commitSha = null, bool isCli = false, CancellationToken ct = default)
    {
        var tspLocationPath = Path.Combine(packagePath, "tsp-location.yaml");
        logger.LogInformation("tsp-client update: {packagePath}, commit: {commit}", packagePath, commitSha ?? "(latest)");
        
        if (!File.Exists(tspLocationPath))
        {
            return new TspToolResponse {
                ResponseError = $"tsp-location.yaml not found at path: {tspLocationPath}",
                TypeSpecProject = packagePath
            };
        }
        
        var repoRootFolder = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
        
        var args = new List<string> { "tsp-client", "update" };
        if (!string.IsNullOrEmpty(commitSha))
        {
            args.Add("--commit");
            args.Add(commitSha);
        }
        
        var npmPrefix = await GetNpmPrefixAsync(repoRootFolder, ct);
        var npmOptions = new NpmOptions(
            npmPrefix,
            args.ToArray(),
            logOutputStream: true,
            workingDirectory: packagePath,
            timeout: TimeSpan.FromMinutes(CommandTimeoutInMinutes)
        );

        var result = await npmHelper.Run(npmOptions, ct);
        if (result.ExitCode != 0)
        {
            return new TspToolResponse
            {
                ResponseError = isCli
                    ? "Failed to regenerate TypeSpec client, see details in the above logs."
                    : "Failed to regenerate TypeSpec client, see generator output below" + Environment.NewLine + result.Output,
                TypeSpecProject = packagePath
            };
        }

        return new TspToolResponse
        {
            IsSuccessful = true,
            TypeSpecProject = packagePath
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

        var repoRootFolder = await gitHelper.DiscoverRepoRootAsync(workingDirectory, ct);
        var npmPrefix = await GetNpmPrefixAsync(repoRootFolder, ct);
        var npmOptions = new NpmOptions(
            npmPrefix,
            arguments.ToArray(),
            logOutputStream: true,
            workingDirectory: workingDirectory,
            timeout: TimeSpan.FromMinutes(CommandTimeoutInMinutes)
        );

        var result = await npmHelper.Run(npmOptions, ct);
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
