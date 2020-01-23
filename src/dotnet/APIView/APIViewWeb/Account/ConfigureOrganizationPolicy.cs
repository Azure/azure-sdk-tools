// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace APIViewWeb
{
    public class ConfigureOrganizationPolicy: IConfigureOptions<AuthorizationOptions>
    {
        private readonly IOptions<OrganizationOptions> _options;

        public ConfigureOrganizationPolicy(IOptions<OrganizationOptions> options)
        {
            _options = options;
        }

        public void Configure(AuthorizationOptions options)
        {
            options.AddPolicy(Startup.RequireOrganizationPolicy, policy =>
            {
                policy.AddRequirements(new OrganizationRequirement(_options.Value.RequiredOrganization));
            });
        }
    }
}