using Azure.Core;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Cdk.Core;

namespace Cdk.AppService
{
    public class WebSitePublishingCredentialPolicy : Resource<CsmPublishingCredentialsPoliciesEntityData>
    {
        private const string ResourceTypeName = "Microsoft.Web/sites/basicPublishingCredentialsPolicies";

        private static string GetName(string? name) => name is null ? $"publishingCredentialPolicy-{Infrastructure.Seed}" : $"{name}-{Infrastructure.Seed}";

        public WebSitePublishingCredentialPolicy(WebSite scope, string resourceName, string version = "2021-02-01", AzureLocation? location = default)
            : base(scope, GetName(resourceName), ResourceTypeName, version, ArmAppServiceModelFactory.CsmPublishingCredentialsPoliciesEntityData(
                name: GetName(resourceName),
                allow: false))
        {
        }
    }
}
