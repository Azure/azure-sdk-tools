// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Models.Responses;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <inheritdoc />
public class TspClientHelper : ITspClientHelper
{
    private readonly INpxHelper npxHelper;
    private readonly ILogger<TspClientHelper> logger;

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
                    : "Failed to convert swagger to TypeSpec project, see generator output below" + Environment.NewLine + result.Output
            };
        }

        return new TspToolResponse
        {
            IsSuccessful = true,
            TypeSpecProjectPath = outputDirectory
        };
    }
}
