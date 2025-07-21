using Microsoft.Extensions.Configuration;

namespace Azure.Tools.GeneratorAgent.Configuration
{
    internal class AppSettings
    {
        private readonly IConfiguration Configuration;

        public AppSettings(IConfiguration configuration)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public string ProjectEndpoint => Configuration.GetSection("AzureSettings:ProjectEndpoint").Value ?? "";
        public string Model => Configuration.GetSection("AzureSettings:Model").Value ?? "gpt-4o";
        public string AgentName => Configuration.GetSection("AzureSettings:AgentName").Value ?? "AZC Fixer";
        public string AgentInstructions => Configuration.GetSection("AzureSettings:AgentInstructions").Value ?? "";
    }
}
