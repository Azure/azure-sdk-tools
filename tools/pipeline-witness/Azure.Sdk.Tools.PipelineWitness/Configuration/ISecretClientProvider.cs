using System;

using Azure.Security.KeyVault.Secrets;

namespace Azure.Sdk.Tools.PipelineWitness.Configuration
{
    public interface ISecretClientProvider
    {
        SecretClient GetSecretClient(Uri vaultUri);
    }
}
