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
        private readonly TokenCredential _credential;
        private readonly ILogger<CopilotAuthenticationService> _logger;

        public CopilotAuthenticationService(IConfiguration configuration, ILogger<CopilotAuthenticationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
         
            _logger.LogWarning("Using ChainedTokenCredential (ManagedIdentity/AzureCli) for Copilot authentication");
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

            string tenantId = _configuration["AzureAd:TenantId"] ?? "72f988bf-86f1-41af-91ab-2d7cd011db47";

        
            var scopeWithPrefix = $"api://{copilotAppId}/.default";
            _logger.LogWarning("Requesting token with scope: {Scope}, tenantId: {TenantId}", scopeWithPrefix, tenantId);
            
            var tokenRequestContext = new TokenRequestContext(
                scopes: new[] { scopeWithPrefix },
                parentRequestId: null,
                claims: null,
                tenantId: tenantId,
                isCaeEnabled: false
            );
            
            var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
            
            _logger.LogWarning("Token acquired, checking audience and roles...");
            LogTokenClaims(token.Token);
            
            // Check if we got the roles claim
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token.Token);
            var hasRoles = jwtToken.Claims.Any(c => c.Type == "roles");
            var audience = jwtToken.Audiences.FirstOrDefault();
            
            if (!hasRoles)
            {
                _logger.LogError("Token does not contain roles claim! Audience: {Audience}", audience);
                _logger.LogError("This suggests managed identity tokens don't support app roles for this resource.");
                _logger.LogError("Managed identity principal: bcb0cf5a-9d34-4ae2-8e9d-c0302c9e7902");
                _logger.LogError("Target resource: {CopilotAppId}", copilotAppId);
            }
            else
            {
                var roles = jwtToken.Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToArray();
                _logger.LogWarning("SUCCESS! Token contains roles: {Roles}", string.Join(", ", roles));
            }
            
            return token.Token;
        }

        private void LogTokenClaims(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                
                _logger.LogWarning("=== TOKEN CLAIMS DECODED ===");
                _logger.LogWarning("  aud (audience): {Audience}", jwtToken.Audiences.FirstOrDefault());
                _logger.LogWarning("  iss (issuer): {Issuer}", jwtToken.Issuer);
                _logger.LogWarning("  appid: {AppId}", jwtToken.Claims.FirstOrDefault(c => c.Type == "appid")?.Value);
                _logger.LogWarning("  oid (object ID): {ObjectId}", jwtToken.Claims.FirstOrDefault(c => c.Type == "oid")?.Value);
                
                // Log roles claim - this is what we're debugging
                var roles = jwtToken.Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToList();
                if (roles.Any())
                {
                    _logger.LogWarning("  roles: [{Roles}]", string.Join(", ", roles));
                }
                else
                {
                    _logger.LogWarning("  roles: NONE - no roles claim found in token!");
                }
                
                // Log all claims for complete picture
                _logger.LogWarning("  === ALL CLAIMS ===");
                foreach (var claim in jwtToken.Claims)
                {
                    _logger.LogWarning("    {Type}: {Value}", claim.Type, claim.Value);
                }
                _logger.LogWarning("=== END TOKEN CLAIMS ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decode token claims");
            }
        }
    }
}
