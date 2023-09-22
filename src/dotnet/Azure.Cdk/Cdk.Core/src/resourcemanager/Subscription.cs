using Azure.Core;
using Azure.Core.Serialization;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Resources;
using Cdk.Core;

namespace Cdk.ResourceManager
{
    public class Subscription : Resource<SubscriptionData>
    {
        internal readonly static ResourceType ResourceType = "Microsoft.Resources/subscriptions";

        private static string GetName(Guid? guid) => guid.HasValue ? guid.Value.ToString() : Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID") ?? throw new InvalidOperationException("No environment variable named 'AZURE_SUBSCRIPTION_ID' found");

        public Subscription(Guid? guid = default)
            : base(
                  Tenant.Instance,
                  GetName(guid),
                  ResourceType,
                  "2022-12-01",
                  ResourceManagerModelFactory.SubscriptionData(
                      id: SubscriptionResource.CreateResourceIdentifier(GetName(guid)),
                      subscriptionId: GetName(guid),
                      tenantId: Tenant.Instance.Properties.TenantId))
        {
        }
    }
}