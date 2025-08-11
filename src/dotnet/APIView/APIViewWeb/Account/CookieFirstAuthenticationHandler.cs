using System;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APIViewWeb.Account;

public class CookieOrBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public CookieOrBearerAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        AuthenticateResult cookieResult = await Context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (cookieResult.Succeeded)
        {
            return cookieResult;
        }

        string authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        string token = authHeader.Substring("Bearer ".Length).Trim();
        if (AuthenticationValidator.IsGitHubToken(token))
        {
            var httpClientFactory = Context.RequestServices.GetRequiredService<IHttpClientFactory>();
            using var httpClient = httpClientFactory.CreateClient();
            ClaimsPrincipal user = await AuthenticationValidator.ValidateGitHubTokenAsync(token, httpClient);
            if (user != null)
            {
                var ticket = new AuthenticationTicket(user, "GitHubToken");
                return AuthenticateResult.Success(ticket);
            }
        }

        AuthenticateResult azureJwtResult = await Context.AuthenticateAsync("Bearer");
        return azureJwtResult.Succeeded ? azureJwtResult : AuthenticateResult.NoResult();
    }
}
