using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Azure.Sdk.Tools.PipelineWitness.Configuration
{
    public class PipelineWitnessSettings
    {
        /// <summary>
        /// Gets or sets the uri of the key vault to use
        /// </summary>
        public string KeyVaultUri { get; set; }

        /// <summary>
        /// Gets or sets uri of the cosmos account to use
        /// </summary>
        public string CosmosAccountUri { get; set; }

        /// <summary>
        /// Gets or sets uri of the storage account to use for queue processing
        /// </summary>
        public string QueueStorageAccountUri { get; set; }

        /// <summary>
        /// Gets or sets uri of the blob storage account to use for blob export
        /// </summary>
        public string BlobStorageAccountUri { get; set; }

        /// <summary>
        /// Gets or sets the name of the build complete queue
        /// </summary>
        public string BuildCompleteQueueName { get; set; }

        /// <summary>
        /// Gets or sets the amount of time a message should be invisible in the queue while being processed
        /// </summary>
        public TimeSpan MessageLeasePeriod { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the amount of time to wait before a failed message can be attempted again
        /// </summary>
        public TimeSpan MessageErrorSleepPeriod { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the amount of time to wait before checking for new messages when the queue is empty
        /// </summary>
        public TimeSpan EmptyQueuePollDelay { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets the number of times a message can be dequeued before being moved to the poison message queue
        /// </summary>
        public long MaxDequeueCount { get; set; } = 5;

        /// <summary>
        /// Gets or sets the list of projects to work with
        /// </summary>
        public string[] Projects { get; set; }

        /// <summary>
        /// Gets or sets the account to work with
        /// </summary>
        public string Account { get; set; }

        /// <summary>
        /// Gets or sets the amount of time between iterations of the build definition upload loop
        /// </summary>
        public TimeSpan BuildDefinitionLoopPeriod { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the number of concurrent build complete queue workers to register
        /// </summary>
        public int BuildCompleteWorkerCount { get; set; } = 1;

        /// <summary>
        /// Gets or sets the artifact name used by the pipeline owners extraction build 
        /// </summary>
        public string PipelineOwnersArtifactName { get; set; }

        /// <summary>
        /// Gets or sets the file name used by the pipeline owners extraction build 
        /// </summary>
        public string PipelineOwnersFilePath { get; set; }

        /// <summary>
        /// Gets or sets the definition id of the pipeline owners extraction build 
        /// </summary>
        public int PipelineOwnersDefinitionId { get; set; }

        /// <summary>
        /// Gets or sets the database to use 
        /// </summary>
        public string CosmosDatabase { get; set; }

        /// <summary>
        /// Gets or sets the container to use for async locks
        /// </summary>
        public string CosmosAsyncLockContainer { get; set; }

        /// <summary>
        /// Gets or sets the authorization key for the Cosmos account
        /// </summary>
        public string CosmosAuthorizationKey { get; set; }

        /// <summary>
        /// Gets or sets the access token to use for Azure DevOps clients
        /// </summary>
        public string DevopsAccessToken { get; set; }
    }
}
