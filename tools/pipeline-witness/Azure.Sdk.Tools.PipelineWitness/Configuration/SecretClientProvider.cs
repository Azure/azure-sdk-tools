using System;

using Azure.Core;
using Azure.Security.KeyVault.Secrets;

namespace Azure.Sdk.Tools.PipelineWitness.Configuration
{
    public class SecretClientProvider : ISecretClientProvider
    {
        private readonly TokenCredential tokenCredential;

        public SecretClientProvider(TokenCredential tokenCredential)
        {
            this.tokenCredential = tokenCredential;
        }

        public SecretClient GetSecretClient(Uri vaultUri)
        {
            return new SecretClient(vaultUri, this.tokenCredential);
        }
    }
}
