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

        public string ProjectEndpoint => _configuration.GetSection("AzureSettings:ProjectEndpoint").Value ?? "";
        public string Model => _configuration.GetSection("AzureSettings:Model").Value ?? "gpt-4o";
        public string AgentName => _configuration.GetSection("AzureSettings:AgentName").Value ?? "AZC Fixer";
        public string AgentInstructions => _configuration.GetSection("AzureSettings:AgentInstructions").Value ?? "";
    }
}
