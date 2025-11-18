using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
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
            
            // Decode and log token claims for debugging
            LogTokenClaims(token.Token);
            
            return token.Token;
        }

        private void LogTokenClaims(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                
                _logger.LogInformation("Token claims decoded:");
                _logger.LogInformation("  aud (audience): {Audience}", jwtToken.Audiences.FirstOrDefault());
                _logger.LogInformation("  iss (issuer): {Issuer}", jwtToken.Issuer);
                _logger.LogInformation("  appid: {AppId}", jwtToken.Claims.FirstOrDefault(c => c.Type == "appid")?.Value);
                _logger.LogInformation("  oid (object ID): {ObjectId}", jwtToken.Claims.FirstOrDefault(c => c.Type == "oid")?.Value);
                
                // Log roles claim - this is what we're debugging
                var roles = jwtToken.Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToList();
                if (roles.Any())
                {
                    _logger.LogInformation("  roles: [{Roles}]", string.Join(", ", roles));
                }
                else
                {
                    _logger.LogWarning("  roles: NONE - no roles claim found in token!");
                }
                
                // Log all claims for complete picture
                _logger.LogInformation("  All claims:");
                foreach (var claim in jwtToken.Claims)
                {
                    _logger.LogInformation("    {Type}: {Value}", claim.Type, claim.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decode token claims");
            }
        }
    }
}
