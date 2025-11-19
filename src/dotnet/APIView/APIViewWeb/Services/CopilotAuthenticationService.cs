using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Services
{
    public class CopilotAuthenticationService : ICopilotAuthenticationService
    {
        private readonly IConfiguration _configuration;
        private readonly ChainedTokenCredential _credential;

        public CopilotAuthenticationService(IConfiguration configuration)
        {
            _configuration = configuration;
            _credential = new ChainedTokenCredential(
                new ManagedIdentityCredential(_configuration["CopilotUserAssignedIdentity"]),
                new AzureCliCredential()
            );
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
