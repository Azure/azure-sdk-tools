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
    public async Task<List<SetupRequirements.Requirement>> GetRequirements(CancellationToken ct = default)
    {
        return await base.GetRequirements("dotnet", ct);
    }
}