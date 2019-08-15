using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb
{
    public class OrganizationRequirementHandler : IAuthorizationHandler
    {
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.Requirements)
            {
                if (requirement is OrganizationRequirement orgRequirement)
                {
                    var claim = context.User.FindFirst("urn:github:orgs");
                    if (claim != null && claim.Value.Split(",").Contains(orgRequirement.RequiredOrganization))
                    {
                        context.Succeed(requirement);
                    }
                }
            }
            return Task.CompletedTask;
        }
    }
}
