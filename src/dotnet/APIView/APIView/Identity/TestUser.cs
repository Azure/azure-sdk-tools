using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace APIView.Identity
{
    public static class TestUser
    {
        public static ClaimsPrincipal GetTestuser()
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
            return new ClaimsPrincipal(identity);
        }
    }
}
