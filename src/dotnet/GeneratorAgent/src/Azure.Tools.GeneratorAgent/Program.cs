using Azure.AI.Agents.Persistent;
using Azure.Core;
using Azure.Identity;
using Azure.Tools.GeneratorAgent.Authentication;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent
{
    public class Program
    {
        static async Task<int> Main(string[] args)
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                cts.Cancel();
                eventArgs.Cancel = true;
            };

            ToolConfiguration ToolConfig = new ToolConfiguration();
            ILoggerFactory LoggerFactory = ToolConfig.CreateLoggerFactory();
            AppSettings AppSettings = ToolConfig.CreateAppSettings();

            ILogger<ErrorFixerAgent> AgentLogger = LoggerFactory.CreateLogger<ErrorFixerAgent>();
            ILogger<CredentialFactory> CredentialLogger = LoggerFactory.CreateLogger<CredentialFactory>();
            ILogger<Program> Logger = LoggerFactory.CreateLogger<Program>();

            try
            {
                RuntimeEnvironment environment = DetermineRuntimeEnvironment();
                TokenCredentialOptions? credentialOptions = CreateCredentialOptions();

                ICredentialFactory credentialFactory = new CredentialFactory(CredentialLogger);
                TokenCredential credential = credentialFactory.CreateCredential(environment, credentialOptions);

                PersistentAgentsAdministrationClient adminClient = new PersistentAgentsAdministrationClient(
                    new Uri(AppSettings.ProjectEndpoint),
                    credential);

                await using (ErrorFixerAgent agent = new ErrorFixerAgent(AppSettings, AgentLogger, adminClient))
                {
                    await agent.FixCodeAsync(cts.Token).ConfigureAwait(false);
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("Operation was cancelled. Shutting down gracefully...");
                return 0;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occurred while running the Generator Agent");
                return 1;
            }
        }

        private static RuntimeEnvironment DetermineRuntimeEnvironment()
        {
            bool isGitHubActions = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
                                 !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_WORKFLOW"));

            if (isGitHubActions)
            {
                return RuntimeEnvironment.DevOpsPipeline;
            }

            return RuntimeEnvironment.LocalDevelopment;
        }

        private static TokenCredentialOptions? CreateCredentialOptions()
        {
            string? tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            Uri? authorityHost = null;

            string? authority = Environment.GetEnvironmentVariable("AZURE_AUTHORITY_HOST");
            if (!string.IsNullOrEmpty(authority) && Uri.TryCreate(authority, UriKind.Absolute, out Uri? parsedAuthority))
            {
                authorityHost = parsedAuthority;
            }

            if (tenantId == null && authorityHost == null)
                return null;

            var options = new TokenCredentialOptions();

            if (authorityHost != null)
            {
                options.AuthorityHost = authorityHost;
            }

            return options;
        }
    }
}
