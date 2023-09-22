using Azure.Core;
using Azure.ResourceManager.KeyVault.Models;
using Cdk.Core;

namespace Cdk.KeyVault
{
    internal class KeyVaultAddAccessPolicy : Resource<KeyVaultAccessPolicyParameters>
    {
        private const string ResourceTypeName = "Microsoft.KeyVault/vaults/accessPolicies";

        public KeyVaultAddAccessPolicy(KeyVault scope, Parameter principalIdParameter, string version = "2023-02-01", AzureLocation? location = default)
            : base(scope, "add", ResourceTypeName, version, ArmKeyVaultModelFactory.KeyVaultAccessPolicyParameters(
                name: "add",
                resourceType: ResourceTypeName,
                accessPolicies: new List<KeyVaultAccessPolicy>
                {
                    new KeyVaultAccessPolicy(
                        Guid.Parse(Environment.GetEnvironmentVariable("AZURE_TENANT_ID")!),
                        principalIdParameter.Name,
                        new IdentityAccessPermissions()
                        {
                            Secrets =
                            {
                                IdentityAccessSecretPermission.Get,
                                IdentityAccessSecretPermission.List
                            }
                        })
                }))
        {
            ParameterOverrides.Add("objectId", principalIdParameter.IsFromOutput ? principalIdParameter.Value! : principalIdParameter.Name);
        }
    }
}
