using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb
{
    public class PullRequestPermissionRequirementHandler : IAuthorizationHandler
    {
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            string[] requiredOrgs = null;
            foreach (var requirement in context.Requirements)
            {
                if (requirement is OrganizationRequirement orgRequirement)
                {
                    requiredOrgs = orgRequirement.RequiredOrganizations;
                }
            }
            if (requiredOrgs != null)
            {
                foreach (var requirement in context.Requirements)
                {
                    // For now permission requirment only validates organization of pull request author.
                    // We may add more permission check in the future.
                    if (requirement is PullRequestPermissionRequirement pullRequirement && context.Resource != null)
                    {
                        var orgs = ((string)context.Resource).Split(",");
                        if (orgs.Any(userOrg => requiredOrgs.Contains(userOrg, StringComparer.OrdinalIgnoreCase)))
                        {
                            context.Succeed(requirement);
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
