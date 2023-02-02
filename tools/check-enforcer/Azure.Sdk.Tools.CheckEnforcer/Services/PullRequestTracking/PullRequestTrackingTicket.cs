using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer.Services.PullRequestTracking
{
    public class PullRequestTrackingTicket : ITableEntity
    {
        public PullRequestTrackingTicket()
        {
        }

        public PullRequestTrackingTicket(long installationId, long repositoryId, int pullRequestNumber)
        {
            this.InstallationId = installationId;
            this.RepositoryId = repositoryId;
            this.PullRequestNumber = pullRequestNumber;
            this.PartitionKey = $"{installationId}.{repositoryId}";
            this.RowKey = $"{this.PartitionKey}.{pullRequestNumber}";
        }

        public long InstallationId { get; set; }
        public long RepositoryId { get; set; }
        public int PullRequestNumber { get; set;  }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
