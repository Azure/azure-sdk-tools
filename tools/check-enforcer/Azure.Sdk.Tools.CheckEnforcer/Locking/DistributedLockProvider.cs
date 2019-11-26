using Azure.Identity;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer.Locking
{
    public class DistributedLockProvider : IDistributedLockProvider
    {
        public DistributedLockProvider(IGlobalConfigurationProvider globalConfigurationProvider)
        {
            this.globalConfigurationProvider = globalConfigurationProvider;
        }

        private IGlobalConfigurationProvider globalConfigurationProvider;
        private BlobServiceClient client;
        private object clientLock = new object();


        public DistributedLock Create(string identifier)
        {
            if (client == null)
            {
                lock(clientLock)
                {
                    if (client == null)
                    {
                        var uri = new Uri(globalConfigurationProvider.GetDistributedLockStorageUri());
                        var credential = new DefaultAzureCredential();
                        client = new BlobServiceClient(uri, credential);

                    }
                }
            }

            var containerName = globalConfigurationProvider.GetDistributedLockContainerName();
            var containerClient = client.GetBlobContainerClient(containerName);

            // TOOD: Consider replacing these with con
            var maxRetries = 3;
            var backoffInterval = TimeSpan.FromMilliseconds(2000);
            var lockDuration = TimeSpan.FromSeconds(20);
            var acquisitionTimeout = TimeSpan.FromSeconds(10);

            var distributedLock = new DistributedLock(
                containerClient,
                identifier,
                maxRetries,
                backoffInterval,
                lockDuration,
                acquisitionTimeout
                );

            return distributedLock;
        }
    }
}
