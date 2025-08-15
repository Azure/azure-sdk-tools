using APIViewWeb.Account;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace APIViewWeb;

public class ConfigureCookieAuthenticationPolicy : IConfigureOptions<AuthorizationOptions>
{
    private readonly IOptions<OrganizationOptions> _options;

    public ConfigureCookieAuthenticationPolicy(IOptions<OrganizationOptions> options)
    {
        _options = options;
    }

    public void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(Startup.RequireCookieAuthenticationPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
            policy.RequireAssertion(context => AuthenticationValidator.HasOrganizationAccess(
                context.User,
                _options.Value.RequiredOrganization));
        });
    }
}
