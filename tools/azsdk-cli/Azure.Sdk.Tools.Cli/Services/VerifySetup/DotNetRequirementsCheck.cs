namespace Azure.Sdk.Tools.Cli.Services.VerifySetup;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.Cli.Models;

public class DotNetRequirementsCheck : EnvRequirementsCheck, IEnvRequirementsCheck
{
    public async Task<List<SetupRequirements.Requirement>> GetRequirements(string packagePath, CancellationToken ct = default)
    {
        return await base.ParseRequirements("dotnet", ct);
    }
}