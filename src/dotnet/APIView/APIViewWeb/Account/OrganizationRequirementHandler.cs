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
        foreach (IAuthorizationRequirement requirement in context.Requirements)
        {
            if (requirement is OrganizationRequirement orgRequirement)
            {
                Claim claim = context.User.FindFirst(ClaimConstants.Orgs);
                if (claim != null)
                {
                    string[] userOrganizations = claim.Value.Split(",");
                    if (userOrganizations.Any(userOrg =>
                            orgRequirement.RequiredOrganizations.Contains(userOrg, StringComparer.OrdinalIgnoreCase)))
                    {
                        context.Succeed(requirement);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
