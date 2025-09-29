using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.ServiceEssentials;
using AuthenticationTicket = Microsoft.AspNetCore.Authentication.AuthenticationTicket;

namespace APIViewWeb.Account;

public class CookieOrTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ILogger<CookieOrTokenAuthenticationHandler> _authLogger;
    private readonly IMiseHandler _miseHandler;

    public CookieOrTokenAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        IMiseHandler miseHandler,
        ILoggerFactory loggerFactory, UrlEncoder encoder)
        : base(options, loggerFactory, encoder)
    {
        _authLogger = loggerFactory.CreateLogger<CookieOrTokenAuthenticationHandler>();
        _miseHandler = miseHandler;
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
            IHttpClientFactory httpClientFactory = Context.RequestServices.GetRequiredService<IHttpClientFactory>();
            using HttpClient httpClient = httpClientFactory.CreateClient();
            ClaimsPrincipal user = await AuthenticationValidator.ValidateGitHubTokenAsync(token, httpClient, _authLogger);
            if (user != null)
            {
                AuthenticationTicket ticket = new(user, "GitHubToken");
                return AuthenticateResult.Success(ticket);
            }
        }

        MiseValidationResult miseOutput = await _miseHandler.ValidateRequest(Request.Headers).ExecuteAsync().ConfigureAwait(false);
        if (miseOutput.HttpResponseStatusCode != 200)
        {
            return AuthenticateResult.Fail(miseOutput.ErrorDescription ?? miseOutput.HttpResponseStatusCode.ToString());
        }

        AuthenticateResult azureJwtResult = await Context.AuthenticateAsync("Bearer");
        return azureJwtResult.Succeeded ? azureJwtResult : AuthenticateResult.NoResult();
    }
}
