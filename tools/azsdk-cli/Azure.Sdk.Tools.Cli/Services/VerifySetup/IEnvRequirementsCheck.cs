using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services.VerifySetup {
    public interface IEnvRequirementsCheck
    {
        Task<List<SetupRequirements.Requirement>> GetRequirements(CancellationToken ct = default);
    }
}

