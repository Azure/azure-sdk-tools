using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent.Authentication
{
    public sealed class CredentialFactory : ICredentialFactory
    {
        private readonly ILogger<CredentialFactory> _logger;

        public CredentialFactory(ILogger<CredentialFactory> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public TokenCredential CreateCredential(RuntimeEnvironment environment, TokenCredentialOptions? options = null)
        {
            _logger.LogInformation("Creating credential for environment {Environment}", environment);

            return environment switch
            {
                RuntimeEnvironment.LocalDevelopment => CreateDevelopmentCredential(options),
                RuntimeEnvironment.DevOpsPipeline => CreatePipelineCredential(options),
                _ => throw new ArgumentException($"Unsupported runtime environment: {environment}", nameof(environment))
            };
        }

        private static TokenCredential CreateDevelopmentCredential(TokenCredentialOptions? options)
        {
            // Get tenant ID from environment (since base options doesn't have it)
            string? tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");

            TokenCredential[] credentials = [
                new AzureCliCredential(new AzureCliCredentialOptions
            {
                TenantId = tenantId,
                AuthorityHost = options?.AuthorityHost
            }),
            new AzurePowerShellCredential(new AzurePowerShellCredentialOptions
            {
                TenantId = tenantId,
                AuthorityHost = options?.AuthorityHost
            }),
            new AzureDeveloperCliCredential(new AzureDeveloperCliCredentialOptions
            {
                TenantId = tenantId,
                AuthorityHost = options?.AuthorityHost
            }),
            new VisualStudioCredential(new VisualStudioCredentialOptions
            {
                TenantId = tenantId,
                AuthorityHost = options?.AuthorityHost
            })
            ];

            return new ChainedTokenCredential(credentials);
        }

        private static TokenCredential CreatePipelineCredential(TokenCredentialOptions? options)
        {

            return new ManagedIdentityCredential(new ManagedIdentityCredentialOptions
            {
                AuthorityHost = options?.AuthorityHost
            });
        }
    }
}
