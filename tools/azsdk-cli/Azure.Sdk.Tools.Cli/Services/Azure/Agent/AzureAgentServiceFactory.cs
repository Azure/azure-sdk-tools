namespace Azure.Sdk.Tools.Cli.Services
{
    public interface IAzureAgentServiceFactory
    {
        IAzureAgentService Create(string? model, string? endpoint);
    }

    public class AzureAgentServiceFactory(IAzureService azureService) : IAzureAgentServiceFactory
    {
        private readonly IAzureService azureService = azureService;

        public IAzureAgentService Create(string? model, string? endpoint)
        {
            return new AzureAgentService(azureService, endpoint, model);
        }
    }
}