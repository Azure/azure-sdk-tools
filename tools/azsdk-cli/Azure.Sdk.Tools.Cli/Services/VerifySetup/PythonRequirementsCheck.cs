namespace Azure.Sdk.Tools.Cli.Services.VerifySetup;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Helpers;
using System.Runtime.InteropServices;

public class PythonRequirementsCheck : EnvRequirementsCheck, IEnvRequirementsCheck
{
    private readonly IProcessHelper processHelper;

    private readonly ILogger<PythonRequirementsCheck> logger;

    public PythonRequirementsCheck(IProcessHelper processHelper, ILogger<PythonRequirementsCheck> logger)
    {
        this.processHelper = processHelper;
        this.logger = logger;
    }

    public async Task<List<SetupRequirements.Requirement>> GetRequirements(string packagePath, CancellationToken ct = default)
    {
        return await GetRequirements(packagePath, null, ct);
    }

    public async Task<List<SetupRequirements.Requirement>> GetRequirements(string packagePath, string venvPath, CancellationToken ct = default)
    {
        var reqs = await base.ParseRequirements("python", ct);

        if (string.IsNullOrWhiteSpace(venvPath))
        {
            venvPath = FindOrCreateVenv(packagePath);
        }

        if (string.IsNullOrWhiteSpace(venvPath) || !Directory.Exists(venvPath))
        {
            logger.LogWarning("Provided venv path is invalid or does not exist: {VenvPath}", venvPath);
            return reqs;
        }
        logger.LogInformation("Using virtual environment at: {VenvPath}", venvPath);
        return UpdateChecksWithVenv(reqs, venvPath);
    }

    private List<SetupRequirements.Requirement> UpdateChecksWithVenv(List<SetupRequirements.Requirement> reqs, string venvPath)
    {
        foreach (var req in reqs)
        {
            // Update checks to use venv path
            var binDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Scripts" : "bin";
            req.check[0] = Path.Combine(venvPath, binDir, req.check[0]);
        }

        return reqs;
    }

    private string FindOrCreateVenv(string packagePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                packagePath = Environment.CurrentDirectory;
            }

            var candidates = new[] { ".venv", "venv", ".env", "env" };

            foreach (var c in candidates)
            {
                var path = Path.Combine(packagePath, c);
                if (Directory.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            // no venv found, create one
            var venvPath = Path.Combine(packagePath, ".venv");

            var processOptions = new ProcessOptions(
                "python",
                new[] { "-m", "venv", venvPath },
                workingDirectory: packagePath
            );

            var result = processHelper.Run(processOptions, CancellationToken.None).GetAwaiter().GetResult();

            if (result.ExitCode == 0 && Directory.Exists(venvPath))
            {
                return Path.GetFullPath(venvPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while trying to find or create a Python virtual environment at path: {PackagePath}", packagePath);
            return null;
        }

        return null;
    }
}
