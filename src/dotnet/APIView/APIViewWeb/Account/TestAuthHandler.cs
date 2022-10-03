using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APIViewWeb.Account
{
    public class TestAuthHandler :  AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "53356347"),
                new Claim(ClaimTypes.Name, "Azure SDK Bot"),
                new Claim(ClaimConstants.Login, "azure-sdk"),
                new Claim(ClaimConstants.Url, "https://api.github.com/users/azure-sdk"),
                new Claim(ClaimConstants.Avatar, "https://avatars.githubusercontent.com/u/53356347?v=4"),
                new Claim(ClaimConstants.Name, "Azure SDK Bot"),
                new Claim(ClaimConstants.Email,"azuresdkengsysteam@microsoft.com"),
                new Claim(ClaimConstants.Orgs, "Azure"),
            };

            var identity = new ClaimsIdentity(claims, "TestUser");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestUser");
            var result = AuthenticateResult.Fail(new Exception("Test Authentication Failed"));

            if (Request.Host.Host.Equals("localhost"))
            {
                result = AuthenticateResult.Success(ticket);
            }

            return Task.FromResult(result);
        }
    }
}
