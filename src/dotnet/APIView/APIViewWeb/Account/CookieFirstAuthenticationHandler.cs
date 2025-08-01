using System;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

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

        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            AuthenticateResult jwtResult = await Context.AuthenticateAsync("Bearer");
            if (jwtResult.Succeeded)
            {
                return jwtResult;
            }
        }

        return AuthenticateResult.NoResult();
    }
}
