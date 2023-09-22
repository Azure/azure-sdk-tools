using Azure.Core;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Cdk.Core;
using Cdk.ResourceManager;

namespace Cdk.AppService
{
    public class AppServicePlan : Resource<AppServicePlanData>
    {
        private const string ResourceTypeName = "Microsoft.Web/serverfarms";

        private static string GetName(string? name) => name is null ? $"appServicePlan-{Infrastructure.Seed}" : $"{name}-{Infrastructure.Seed}";

        public AppServicePlan(ResourceGroup scope, string resourceName, string version = "2021-02-01", AzureLocation? location = default)
            : base(scope, GetName(resourceName), ResourceTypeName, version, ArmAppServiceModelFactory.AppServicePlanData(
                name: GetName(resourceName),
                location: GetLocation(location),
                sku: new AppServiceSkuDescription() { Name = "B1" },
                isReserved: true))
        {
        }
    }
}
