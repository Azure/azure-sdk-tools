// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIViewWeb.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace APIViewWeb;

public class ConfigureTokenAuthenticationPolicy : IConfigureOptions<AuthorizationOptions>
{
    private readonly IOptions<OrganizationOptions> _options;

    public ConfigureTokenAuthenticationPolicy(IOptions<OrganizationOptions> options)
    {
        _options = options;
    }

    public void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(Startup.RequireTokenAuthenticationPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddAuthenticationSchemes("TokenAuth", "Bearer");
            policy.RequireAssertion(context =>
            {
                if (context.User.Identity?.AuthenticationType == "Cookies")
                {
                    return false;
                }

                return AuthenticationValidator.HasOrganizationOrAzureAuthenticationAccess(context.User,
                    _options.Value.RequiredOrganization);
            });
        });
    }
}
