using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using APIView.Identity;
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
            var principal = TestUser.GetTestuser();
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
