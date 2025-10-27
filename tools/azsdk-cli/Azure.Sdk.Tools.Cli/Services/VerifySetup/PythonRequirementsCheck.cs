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

public class PythonRequirementsCheck : IEnvRequirementsCheck
{
    private readonly IProcessHelper processHelper;

    private readonly ILogger<PythonRequirementsCheck> logger;

    public PythonRequirementsCheck(IProcessHelper processHelper, ILogger<PythonRequirementsCheck> logger)
    {
        this.processHelper = processHelper;
        this.logger = logger;
    }

    public List<SetupRequirements.Requirement> GetRequirements(string packagePath, Dictionary<string, List<SetupRequirements.Requirement>> categories, CancellationToken ct = default)
    {
        return GetRequirements(packagePath, categories, null, ct);
    }

    public List<SetupRequirements.Requirement> GetRequirements(string packagePath, Dictionary<string, List<SetupRequirements.Requirement>> categories, string venvPath, CancellationToken ct = default)
    {
        var reqs = categories.TryGetValue("python", out var requirements) ? requirements : new List<SetupRequirements.Requirement>();

        if (string.IsNullOrWhiteSpace(venvPath))
        {
            venvPath = FindVenv(packagePath);
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

    private string FindVenv(string packagePath)
    {
        // try to find existing venv in package path if none was provided
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
        
        return null;
    }
}
