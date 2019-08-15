using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIViewWeb
{
    public static class GitHubUserExtension
    {
        public static string GetGitHubLogin(this ClaimsPrincipal user)
        {
            return user.FindFirst(c => c.Type == "urn:github:login")?.Value;
        }
    }
    public class OrganizationRequirement: IAuthorizationRequirement
    {
        public string Org { get; set; }
        public OrganizationRequirement(string org)
        {
            Org = org;
        }
    }

    public class OrganizationRequirementHandler : IAuthorizationHandler
    {
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var r in context.Requirements)
            {
                if (r is OrganizationRequirement o)
                {
                    try
                    {
                        if (context.User.FindFirst("urn:github:orgs").Value.Split(",").Contains(o.Org))
                        {
                            context.Succeed(r);
                        }
                    } catch { }
                }
            }
            return Task.CompletedTask;
        }
    }
    public static class AuthorizationPolicyBuilderExtension
    {
        public static AuthorizationPolicyBuilder RequireOrganizationRequirement(this AuthorizationPolicyBuilder policy, string org)
        {
            policy.RequireClaim("urn:github:orgs");
            return policy.AddRequirements(new OrganizationRequirement(org));
        }
    }
}
