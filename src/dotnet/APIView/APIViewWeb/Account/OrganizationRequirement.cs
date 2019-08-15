using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class OrganizationRequirement : IAuthorizationRequirement
    {
        public string RequiredOrganization { get; set; }

        public OrganizationRequirement(string requiredOrganization)
        {
            RequiredOrganization = requiredOrganization;
        }
    }
}
