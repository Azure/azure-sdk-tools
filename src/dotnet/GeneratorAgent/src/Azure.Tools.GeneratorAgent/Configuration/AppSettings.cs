using Microsoft.Extensions.Configuration;

namespace Azure.Tools.GeneratorAgent.Configuration
{
    public class AppSettings
    {
        private readonly IConfiguration _configuration;

        public AppSettings(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string ProjectEndpoint => _configuration.GetValue<string>("AzureSettings:ProjectEndpoint") ?? "";
        public string Model => _configuration.GetValue<string>("AzureSettings:Model") ?? "gpt-4o";
        public string AgentName => _configuration.GetValue<string>("AzureSettings:AgentName") ?? "AZC Fixer";
        public string AgentInstructions => _configuration.GetValue<string>("AzureSettings:AgentInstructions");

    }
}

