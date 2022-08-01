using System;

using Azure.Security.KeyVault.Secrets;

namespace Azure.Sdk.Tools.PipelineOwnersExtractor.Configuration
{
    public interface ISecretClientProvider
    {
        SecretClient GetSecretClient(Uri vaultUri);
    }
}