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
        private readonly TokenCredential _credential;
        private readonly ILogger<CopilotAuthenticationService> _logger;

        public CopilotAuthenticationService(IConfiguration configuration, ILogger<CopilotAuthenticationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            _credential = new ChainedTokenCredential(
                new ManagedIdentityCredential(_configuration["CopilotUserAssignedIdentity"] ),
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

            string tenantId = _configuration["AzureAd:TenantId"];
            
            var tokenRequestContext = new TokenRequestContext(
                scopes: new[] { $"api://{copilotAppId}/.default" },
                parentRequestId: null,
                claims: null,
                tenantId: tenantId,
                isCaeEnabled: false
            );

            AccessToken token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
            return token.Token;
        }
    }
}
