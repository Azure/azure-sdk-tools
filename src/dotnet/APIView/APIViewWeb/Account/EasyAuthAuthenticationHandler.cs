using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace APIViewWeb.Account;

public class EasyAuthAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ILogger<EasyAuthAuthenticationHandler> _logger;

    public EasyAuthAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder)
        : base(options, loggerFactory, encoder)
    {
        _logger = loggerFactory.CreateLogger<EasyAuthAuthenticationHandler>();
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            if (!Request.Headers.ContainsKey("X-MS-CLIENT-PRINCIPAL"))
            {
                _logger.LogDebug("X-MS-CLIENT-PRINCIPAL header not found");
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            ClaimsPrincipal principal = CreateClaimsPrincipalFromEasyAuth();
            if (principal == null)
            {
                _logger.LogWarning("Failed to create claims principal from Easy Auth headers");
                return Task.FromResult(AuthenticateResult.Fail("Invalid Easy Auth headers"));
            }

            AuthenticationTicket ticket = new(principal, "EasyAuth");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Easy Auth authentication");
            return Task.FromResult(AuthenticateResult.Fail("Easy Auth processing failed"));
        }
    }

    private ClaimsPrincipal CreateClaimsPrincipalFromEasyAuth()
    {

        if (!Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out StringValues principalHeader))
        {
            return null;
        }

        try
        {
            string principalJson = Encoding.UTF8.GetString(Convert.FromBase64String(principalHeader.First()));
            EasyAuthPrincipal principal = JsonSerializer.Deserialize<EasyAuthPrincipal>(principalJson);

            if (principal?.Claims == null || principal.IdentityProvider != "aad")
            {
                return null;
            }

            EasyAuthClaim oidClaim = principal.Claims.FirstOrDefault(c => 
                c.Type == "oid" || c.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier");
            if (oidClaim == null)
            {
                return null;
            }

            List<Claim> claims =
            [
                new("oid", oidClaim.Value),
                new("auth_method", "aad")
            ];

            ClaimsIdentity identity = new(claims, "EasyAuth");
            return new ClaimsPrincipal(identity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Easy Auth principal JSON");
            return null;
        }
    }

    private class EasyAuthPrincipal
    {
        public List<EasyAuthClaim> Claims { get; set; }
        public string? IdentityProvider { get; set; }
    }

    private class EasyAuthClaim
    {
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
