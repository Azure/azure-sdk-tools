namespace Azure.Sdk.Tools.Cli.Services.VerifySetup;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Azure.Sdk.Tools.Cli.Models;

public abstract class EnvRequirementsCheck
{
    public async Task<List<SetupRequirements.Requirement>> ParseRequirements(string checkCategory, CancellationToken ct = default)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames();
        using var stream = assembly.GetManifestResourceStream("Azure.Sdk.Tools.Cli.Configuration.RequirementsV1.json");
        using var reader = new StreamReader(stream);
        var requirementsJson = await reader.ReadToEndAsync(ct);
        var setupRequirements = JsonSerializer.Deserialize<SetupRequirements>(requirementsJson);

        if (setupRequirements == null)
        {
            throw new Exception("Failed to parse requirements JSON.");
        }

        var reqs = new List<SetupRequirements.Requirement>();
        foreach (var kv in setupRequirements.categories)
        {
            var category = kv.Key;
            var requirements = kv.Value;
            if (string.Equals(category, checkCategory, StringComparison.OrdinalIgnoreCase))
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
