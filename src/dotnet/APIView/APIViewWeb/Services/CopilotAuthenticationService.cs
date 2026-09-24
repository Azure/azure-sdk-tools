using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Services
{
    public class CopilotAuthenticationService : ICopilotAuthenticationService
    {
        private readonly IConfiguration _configuration;
        private readonly TokenCredential _credential;

        public CopilotAuthenticationService(IConfiguration configuration)
        {
            _configuration = configuration;

            string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                              Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            _credential = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)
                ? new ChainedTokenCredential(new AzureCliCredential(), new AzureDeveloperCliCredential())
                : new ManagedIdentityCredential(_configuration["CopilotUserAssignedIdentity"]);
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            string copilotAppId = _configuration["CopilotAppId"];
            if (string.IsNullOrEmpty(copilotAppId))
            {
                throw new InvalidOperationException("CopilotAppId configuration is missing");
            }

            var tokenRequestContext = new TokenRequestContext(
                scopes: new[] { $"api://{copilotAppId}/.default" },
                parentRequestId: null,
                claims: null,
                tenantId: _configuration["AzureAd:TenantId"],
                isCaeEnabled: false
            );

            AccessToken token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
            return token.Token;
        }
    }
}
