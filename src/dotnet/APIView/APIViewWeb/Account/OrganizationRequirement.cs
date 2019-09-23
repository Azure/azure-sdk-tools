using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class OrganizationRequirement : IAuthorizationRequirement
    {
        public string[] RequiredOrganizations { get; set; }

        public OrganizationRequirement(string[] requiredOrganizations)
        {
            RequiredOrganizations = requiredOrganizations;
        }
    }
}
