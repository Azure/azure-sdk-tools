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
        private readonly ChainedTokenCredential _credential;

        public CopilotAuthenticationService(IConfiguration configuration)
        {
            _configuration = configuration;
            _credential = new ChainedTokenCredential(
                new ManagedIdentityCredential(),
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

            var scope = $"api://{copilotAppId}/.default";
            var tokenRequestContext = new TokenRequestContext([scope]);
            var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
            return token.Token;
        }
    }
}
