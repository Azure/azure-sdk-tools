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

            // For managed identity tokens to include app roles, we need to ensure the token
            // audience (aud claim) is the full Application ID URI (api://guid), not just the GUID.
            // 
            // Azure IMDS has a quirk: when you request api://guid/.default, it sometimes returns
            // a token with aud=guid (no api:// prefix), which doesn't include app roles.
            //
            // We'll try multiple approaches to get the right audience in the token.
            
            // Approach 1: Try with full URI
            var scopeWithPrefix = $"api://{copilotAppId}/.default";
            _logger.LogWarning("Attempt 1: Requesting token with scope: {Scope}", scopeWithPrefix);
            
            var tokenRequestContext = new TokenRequestContext(new[] { scopeWithPrefix });
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
                _logger.LogError("This means app role assignments are not being included in the token.");
                _logger.LogError("Managed identity principal: bcb0cf5a-9d34-4ae2-8e9d-c0302c9e7902");
                _logger.LogError("Target resource: {CopilotAppId}", copilotAppId);
                _logger.LogError("App roles SHOULD be assigned but are not appearing in the token.");
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
