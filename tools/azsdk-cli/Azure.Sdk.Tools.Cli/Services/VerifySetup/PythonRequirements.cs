namespace Azure.Sdk.Tools.Cli.Services.VerifySetup;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.Models;

public class PythonRequirementsCheck : IEnvRequirementsCheck
{
    private readonly string PATH_TO_REQS = Path.Combine(AppContext.BaseDirectory, "Configuration", "RequirementsV1.json");

    public async Task<List<SetupRequirements.Requirement>> GetRequirements(CancellationToken ct = default)
    {
        var requirementsJson = await File.ReadAllTextAsync(PATH_TO_REQS, ct);
        var setupRequirements = JsonSerializer.Deserialize<SetupRequirements>(requirementsJson);

        if (setupRequirements == null)
        {
            throw new Exception("Failed to parse requirements JSON.");
        }

        // The JSON model in SetupRequirements has categories -> list of Requirement
        var reqs = new List<SetupRequirements.Requirement>();
        foreach (var kv in setupRequirements.categories)
        {
            var category = kv.Key;
            var requirements = kv.Value;
            if (string.Equals(category, "core", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(category, "python", StringComparison.OrdinalIgnoreCase))
            {
                if (requirements != null)
                {
                    reqs.AddRange(requirements);
                }
            }
        }

        return reqs;
    }
}