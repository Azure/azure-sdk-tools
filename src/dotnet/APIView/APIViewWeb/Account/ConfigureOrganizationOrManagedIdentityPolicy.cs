// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Security.Claims;
using APIViewWeb.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            {
                // Get logger from the service provider
                var logger = context.Resource is HttpContext httpContext 
                    ? httpContext.RequestServices.GetService<ILogger<ConfigureOrganizationOrManagedIdentityPolicy>>()
                    : null;

                logger?.LogInformation("Executing RequireOrganizationOrManagedIdentityPolicy for user: {UserName}", 
                    context.User?.Identity?.Name ?? "Anonymous");
                
                logger?.LogInformation("Required organizations: {RequiredOrgs}", 
                    string.Join(", ", _options.Value.RequiredOrganization ?? Array.Empty<string>()));

                var result = AuthenticationValidator.HasOrganizationOrManagedIdentityAccess(
                    context.User, 
                    _options.Value.RequiredOrganization, 
                    logger);

                logger?.LogInformation("Authorization policy result: {Result}", result);
                
                return result;
            });
        });
    }
}
