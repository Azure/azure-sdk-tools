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
         
            // Use federated identity: managed identity → app registration (with app roles!)
            string tenantId = _configuration["AzureAd:TenantId"] ;
            string clientId = _configuration["AzureAd:ClientId"];
            
            _logger.LogWarning("🔐 Using ManagedIdentityCredential with Federated Identity");
            _logger.LogWarning("🔐 Managed Identity → App Registration: {ClientId}", clientId);
            _logger.LogWarning("🔐 This will get app roles: App.Write, App.Read");
            
            // Create credential that uses managed identity to get token for the app registration
            // The federated credential we created allows this exchange
            _credential = new ManagedIdentityCredential(clientId);
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
            _logger.LogWarning("🎫 Scope: {Scope}, TenantId: {TenantId}", scopeWithPrefix, tenantId);
            
            var tokenRequestContext = new TokenRequestContext(
                scopes: new[] { scopeWithPrefix },
                parentRequestId: null,
                claims: null,
                tenantId: tenantId,
                isCaeEnabled: false
            );

            AccessToken token;
            try
            {
                 token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
            _logger.LogWarning("✅ Token acquired, checking audience and roles...");
            LogTokenClaims(token.Token);
            
            // Check if we got the roles claim
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token.Token);
            var hasRoles = jwtToken.Claims.Any(c => c.Type == "roles");
            var audience = jwtToken.Audiences.FirstOrDefault();
            var oid = jwtToken.Claims.FirstOrDefault(c => c.Type == "oid")?.Value;
            var appid = jwtToken.Claims.FirstOrDefault(c => c.Type == "appid")?.Value;
            
            if (!hasRoles)
            {
                _logger.LogError("❌ Token does NOT contain roles claim!");
                _logger.LogError("❌ Object ID (oid): {ObjectId}", oid);
                _logger.LogError("❌ App ID (appid): {AppId}", appid);
                _logger.LogError("❌ Expected App ID: 51ca54a9-657b-4c49-a58c-5a0a59f2cc0c (APIView UX)");
                _logger.LogError("❌ Federated credential may not be working correctly!");
            }
            else
            {
                var roles = jwtToken.Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToArray();
                _logger.LogWarning("✅✅✅ SUCCESS! Token contains roles: {Roles}", string.Join(", ", roles));
                _logger.LogWarning("✅ Object ID (oid): {ObjectId}", oid);
                _logger.LogWarning("✅ App ID (appid): {AppId} - Should be APIView (UX) app registration", appid);
                _logger.LogWarning("✅ Expected App ID: 51ca54a9-657b-4c49-a58c-5a0a59f2cc0c");
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
