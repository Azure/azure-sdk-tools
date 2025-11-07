using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent.Authentication
{
    internal class CredentialFactory
    {
        private readonly ILogger<CredentialFactory> Logger;

        public CredentialFactory(ILogger<CredentialFactory> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            Logger = logger;
        }

        public TokenCredential CreateCredential(RuntimeEnvironment environment, TokenCredentialOptions? options = null)
        {
            Logger.LogDebug("Creating credential for environment {Environment}", environment);

            return environment switch
            {
                RuntimeEnvironment.LocalDevelopment => CreateDevelopmentCredential(options),
                RuntimeEnvironment.DevOpsPipeline => CreatePipelineCredential(options),
                _ => throw new ArgumentException($"Unsupported runtime environment: {environment}", nameof(environment))
            };
        }

        private static TokenCredential CreateDevelopmentCredential(TokenCredentialOptions? options)
        {
            string? tenantId = Environment.GetEnvironmentVariable(EnvironmentVariables.AzureTenantId);

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

        private static TokenCredential CreatePipelineCredential(TokenCredentialOptions? options) =>
            new ManagedIdentityCredential(new ManagedIdentityCredentialOptions
            {
                AuthorityHost = options?.AuthorityHost
            });
    }
}
