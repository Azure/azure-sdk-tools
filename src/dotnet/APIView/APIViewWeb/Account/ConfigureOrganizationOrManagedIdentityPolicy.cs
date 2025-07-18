// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Security.Claims;
using APIViewWeb.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace APIViewWeb;

public class ConfigureOrganizationOrManagedIdentityPolicy : IConfigureOptions<AuthorizationOptions>
{
    private readonly IOptions<OrganizationOptions> _options;

    public ConfigureOrganizationOrManagedIdentityPolicy(IOptions<OrganizationOptions> options)
    {
        _options = options;
    }

    public void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(Startup.RequireOrganizationPolicy, policy =>
        {
            policy.AddRequirements(new OrganizationRequirement(_options.Value.RequiredOrganization));
        });

        options.AddPolicy(Startup.RequireOrganizationOrManagedIdentityPolicy, policy =>
        {
            policy.RequireAssertion(context =>
                AuthenticationValidator.HasOrganizationAccess(context.User, _options.Value.RequiredOrganization) ||
                AuthenticationValidator.IsValidManagedIdentity(context.User));
        });
    }
}
