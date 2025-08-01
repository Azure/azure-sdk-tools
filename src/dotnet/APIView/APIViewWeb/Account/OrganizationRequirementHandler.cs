using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using APIView.Identity;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb;

public class OrganizationRequirementHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        Claim orgClaim = context.User.FindFirst(ClaimConstants.Orgs);
        if (orgClaim == null)
        {
            return Task.CompletedTask;
        }

        string[] userOrganizations = orgClaim.Value.Split(",");
        foreach (IAuthorizationRequirement requirement in context.Requirements)
        {
            if (requirement is OrganizationRequirement orgRequirement &&
                userOrganizations.Any(userOrg =>
                    orgRequirement.RequiredOrganizations.Contains(userOrg, StringComparer.OrdinalIgnoreCase)))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
