using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb
{
    public class PullRequestPermissionRequirementHandler : IAuthorizationHandler
    {
        private string[] requiredOrgs;

        public PullRequestPermissionRequirementHandler(IConfiguration configuration, IOptions<OrganizationOptions> options)
        {
            requiredOrgs = options.Value.RequiredOrganization;
        }

        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.Requirements)
            {
                // For now permission requirment only validates organization of pull request author.
                // We may add more permission check in the future.
                if (requirement is PullRequestPermissionRequirement pullRequirement)
                {
                    var orgs = (IEnumerable<string>)context.Resource;
                    if (orgs.Any(userOrg => requiredOrgs.Contains(userOrg, StringComparer.OrdinalIgnoreCase)))
                    {
                        context.Succeed(requirement);
                    }
                }
            }
            return Task.CompletedTask;
        }
    }
}
