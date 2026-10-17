using APIViewWeb.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace APIViewWeb;

public class ConfigureCookieOrTokenPolicy : IConfigureOptions<AuthorizationOptions>
{
    private readonly IOptions<OrganizationOptions> _options;

    public ConfigureCookieOrTokenPolicy(IOptions<OrganizationOptions> options)
    {
        _options = options;
    }

    public void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(Startup.RequireTokenOrCookieAuthenticationPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context =>
                AuthenticationValidator.HasOrganizationOrAzureAuthenticationAccess(context.User,
                    _options.Value.RequiredOrganization));
        });
    }
}
