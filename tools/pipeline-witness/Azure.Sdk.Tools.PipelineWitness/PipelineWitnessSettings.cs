using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Azure.Sdk.Tools.PipelineWitness
{
    public class PipelineWitnessSettings
    {
        public string KeyVaultUri { get; set; }

        public string QueueStorageAccountUri { get; set; }


        public string BlobStorageAccountUri { get; set; }

        /// <summary>
        /// Gets or sets the name of the build complete queue
        /// </summary>
        public string BuildCompleteQueueName { get; set; }

        /// <summary>
        /// Gets or sets the name of the build log bundles queue
        /// </summary>
        public string BuildLogBundlesQueueName { get; set; }

        /// <summary>
        /// Gets or sets the number of build logs to add to each log bundle message
        /// </summary>
        public int BuildLogBundleSize { get; set; } = 50;

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
        /// Gets or sets the number of concurrent log bundle queue workers to register
        /// </summary>
        public int BuildLogBundlesWorkerCount { get; set; } = 1;

        /// <summary>
        /// Gets or sets the number of concurrent build complete queue workers to register
        /// </summary>
        public int BuildCompleteWorkerCount { get; set; } = 1;
    }
}
