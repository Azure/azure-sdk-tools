using Azure.Core;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.KeyVault.Models;
using Cdk.Core;
using Cdk.ResourceManager;

namespace Cdk.KeyVault
{
    public class KeyVault : Resource<KeyVaultData>
    {
        private const string ResourceTypeName = "Microsoft.KeyVault/vaults";

        public KeyVault(ResourceGroup scope, string name, string version = "2023-02-01", AzureLocation? location = default)
            : base(scope, GetName(name), ResourceTypeName, version, ArmKeyVaultModelFactory.KeyVaultData(
                name: GetName(name),
                resourceType: ResourceTypeName,
                location: GetLocation(location),
                properties: ArmKeyVaultModelFactory.KeyVaultProperties(
                    tenantId: Tenant.Instance.Properties.TenantId!.Value,
                    sku: new KeyVaultSku(KeyVaultSkuFamily.A, KeyVaultSkuName.Standard),
                    accessPolicies: Environment.GetEnvironmentVariable("AZURE_PRINCIPAL_ID") is not null ? new List<KeyVaultAccessPolicy>()
                    {
                        new KeyVaultAccessPolicy(Tenant.Instance.Properties.TenantId!.Value, Environment.GetEnvironmentVariable("AZURE_PRINCIPAL_ID"), new IdentityAccessPermissions()
                        {
                            Secrets =
                            {
                                IdentityAccessSecretPermission.Get,
                                IdentityAccessSecretPermission.List
                            }
                        })
                    } : default)))
        {
        }

        private static string GetName(string? name)
        {
            return name is null ? $"kv-{Infrastructure.Seed}" : $"{name}-{Infrastructure.Seed}";
        }

        public void AddAccessPolicy(Output output)
        {
            var accessPolicy = new KeyVaultAddAccessPolicy(this, new Parameter(output));

            output.Source.Resources.Add(accessPolicy);
            output.Source.ResourceReferences.Add(this);
            Resources.Remove(accessPolicy);
        }
    }
}
