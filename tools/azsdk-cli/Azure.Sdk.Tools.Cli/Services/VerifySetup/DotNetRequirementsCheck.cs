namespace Azure.Sdk.Tools.Cli.Services.VerifySetup;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.Models;

public class DotNetRequirementsCheck : IEnvRequirementsCheck
{
    public List<SetupRequirements.Requirement> GetRequirements(string packagePath, Dictionary<string, List<SetupRequirements.Requirement>> categories, CancellationToken ct = default)
    {
        return categories.TryGetValue("dotnet", out var requirements) ? requirements : new List<SetupRequirements.Requirement>();
    }
}
