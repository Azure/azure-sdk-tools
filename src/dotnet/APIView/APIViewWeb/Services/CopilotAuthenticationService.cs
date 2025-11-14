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
        private readonly ILogger<CopilotAuthenticationService> _logger;

        public CopilotAuthenticationService(IConfiguration configuration, ILogger<CopilotAuthenticationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            // Check if user-assigned managed identity is configured
            var managedIdentityClientId = configuration["ManagedIdentityClientId"] ?? 
                                          configuration["AZURE_CLIENT_ID"] ?? 
                                          Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            
            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("Initializing Copilot authentication with user-assigned Managed Identity. ClientId: {ClientId}", managedIdentityClientId);
                _credential = new ChainedTokenCredential(
                    new ManagedIdentityCredential(managedIdentityClientId),
                    new AzureCliCredential()
                );
            }
            else
            {
                _logger.LogInformation("Initializing Copilot authentication with system-assigned Managed Identity (no client ID configured)");
                _credential = new ChainedTokenCredential(
                    new ManagedIdentityCredential(),
                    new AzureCliCredential()
                );
            }
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            string copilotAppId = _configuration["CopilotAppId"];
            if (string.IsNullOrEmpty(copilotAppId))
            {
                _logger.LogError("CopilotAppId configuration is missing");
                throw new InvalidOperationException("CopilotAppId configuration is missing");
            }

            var scope = $"api://{copilotAppId}/.default";
            _logger.LogInformation("Requesting Copilot access token. CopilotAppId: {CopilotAppId}, Scope: {Scope}", copilotAppId, scope);

            try
            {
                var tokenRequestContext = new TokenRequestContext([scope]);
                var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
                
                // Log token details (but not the actual token value)
                _logger.LogInformation(
                    "Successfully obtained Copilot access token. ExpiresOn: {ExpiresOn}, HasToken: {HasToken}", 
                    token.ExpiresOn, 
                    token.Token.Length > 0);
                
                return token.Token;
            }
            catch (AuthenticationFailedException ex)
            {
                _logger.LogError(ex, 
                    "Failed to obtain Copilot access token. Scope: {Scope}, CopilotAppId: {CopilotAppId}, Error: {ErrorMessage}", 
                    scope, copilotAppId, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Unexpected error obtaining Copilot access token. Scope: {Scope}, CopilotAppId: {CopilotAppId}", 
                    scope, copilotAppId);
                throw;
            }
        }
    }
}
