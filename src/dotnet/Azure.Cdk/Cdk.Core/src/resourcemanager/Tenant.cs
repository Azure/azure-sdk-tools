using Azure.Core;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Resources;
using Cdk.Core;

namespace Cdk.ResourceManager
{
    public class Tenant : Resource<TenantData>
    {
        private const string ResourceTypeName = "Microsoft.Resources/tenants";

        private static string GetName() => Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? throw new InvalidOperationException("No environment variable named 'AZURE_TENANT_ID' found");

        private static readonly object _lock = new object();
        private static Tenant? _instance;

        public static Tenant Instance
        {
            get
            {
                if(_instance is null)
                {
                    lock(_lock)
                    {
                        if(_instance is null)
                        {
                            _instance = new Tenant(
                                null,
                                GetName(),
                                ResourceTypeName,
                                "2022-12-01",
                                ResourceManagerModelFactory.TenantData(
                                    tenantId: Guid.Parse(GetName())));
                        }
                    }
                }
                return _instance;
            }
        }

        private Tenant(Resource? scope, string resourceName, ResourceType resourceType, string version, TenantData properties)
            : base(scope, resourceName, resourceType, version, properties)
        {
        }
    }
}
