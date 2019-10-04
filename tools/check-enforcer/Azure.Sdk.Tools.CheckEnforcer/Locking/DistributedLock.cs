using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Locking
{
    public class DistributedLock : IDisposable
    {
        private BlobContainerClient containerClient;
        private string lockIdentifier;
        private int maxRetries;
        private TimeSpan backoffInterval;
        private TimeSpan lockDuration;
        private TimeSpan acquisitionTimeout;
        private int attempts;
        private LeaseClient leaseClient;
        private static Random random = new Random();

        public DistributedLock(BlobContainerClient containerClient, string lockIdentifier, int maxRetries, TimeSpan backoffInterval, TimeSpan lockDuration, TimeSpan acquisitionTimeout)
        {
            this.containerClient = containerClient;
            this.lockIdentifier = lockIdentifier;
            this.maxRetries = maxRetries;
            this.backoffInterval = backoffInterval;
            this.lockDuration = lockDuration;
            this.acquisitionTimeout = acquisitionTimeout;
        }

        public async Task AcquireAsync()
        {
            var acquisitionStarted = DateTimeOffset.UtcNow;

            do
            {
                attempts++;

                var blobClient = containerClient.GetBlobClient(lockIdentifier);
                leaseClient = blobClient.GetLeaseClient();

                try
                {
                    await leaseClient.AcquireAsync(lockDuration);
                    return;
                }
                catch (StorageRequestFailedException ex) when (ex.ErrorCode == "BlobNotFound")
                {
                    var contentBytes = Encoding.UTF8.GetBytes(lockIdentifier);
                    var contentStream = new MemoryStream(contentBytes);
                    await blobClient.UploadAsync(contentStream);
                }
                catch (StorageRequestFailedException ex) when (ex.ErrorCode == "LeaseAlreadyPresent")
                {
                    // Simple backoff logic.
                    var immediateMultiplier = random.Next(0, 2);
                    int delay = immediateMultiplier * attempts * ((int)backoffInterval.TotalMilliseconds);
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    throw ex;
                }

            } while (attempts < maxRetries || DateTimeOffset.UtcNow > acquisitionStarted + acquisitionTimeout);

            throw new CheckEnforcerDistributedLockException($"Failed to acquire lock for {lockIdentifier}.");
        }

        public async Task ReleaseAsync()
        {
            if (leaseClient != null)
            {
                await leaseClient.ReleaseAsync();
                leaseClient = null;
            }
        }

        public void Dispose()
        {
            ReleaseAsync().Wait(10000);
        }
    }

}
