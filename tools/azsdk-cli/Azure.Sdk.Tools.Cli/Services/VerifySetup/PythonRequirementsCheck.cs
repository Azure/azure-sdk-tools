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
        var reqs = await base.ParseRequirements("python", ct);

        // use venv
        var venvPath = FindOrCreateVenv(packagePath);

        logger.LogInformation("Using virtual environment at: {VenvPath}", venvPath);
        
        if (venvPath == null)
        {
            logger.LogWarning("No venv path determined for Python requirements check");
            return reqs;
        }

        // update checks to use venv path
        foreach (var req in reqs)
        {
            var binDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Scripts" : "bin";
            req.check[0] = Path.Combine(venvPath, binDir, req.check[0]);
        }

        return reqs;
    }

    private string? FindOrCreateVenv(string packagePath)
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
        catch
        {
            return null;
        }

        return null;
    }
}
