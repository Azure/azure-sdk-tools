namespace Azure.Sdk.Tools.Cli.Services;

public interface IAzureAgentServiceFactory
{
    IAzureAgentService Create(string? projectEndpoint = null, string? model = null);
}

public class AzureAgentServiceFactory(IAzureService azureService, ILogger<AzureAgentService> logger) : IAzureAgentServiceFactory
{
    public IAzureAgentService Create(string? projectEndpoint = null, string? model = null)
    {
        return new AzureAgentService(azureService, logger, projectEndpoint, model);
    }
}
