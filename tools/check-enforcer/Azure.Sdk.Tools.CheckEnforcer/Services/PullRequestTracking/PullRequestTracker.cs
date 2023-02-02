using Azure.Data.Tables;
using Azure.Security.KeyVault.Secrets;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Services.PullRequestTracking
{
    public class PullRequestTracker : IPullRequestTracker
    {
        private SecretClient secretClient;

        public PullRequestTracker(SecretClient secretClient)
        {
            this.secretClient = secretClient;
        }

        private const string ConnectionStringSecretName = "table-storage-connection-string";
        private const string PullRequestStateTableName = "pullrequests";
        private object tableClientLock = new object();
        private TableClient tableClient;

        private TableClient GetTableClient()
        {
            if (tableClient == null)
            {
                lock (tableClientLock)
                {
                    if (tableClient == null)
                    {
                        KeyVaultSecret secret = secretClient.GetSecret(ConnectionStringSecretName);
                        var connectionString = secret.Value;

                        tableClient = new TableClient(connectionString, PullRequestStateTableName);
                    }
                }
            }

            return tableClient;
        }

        public async Task StartTrackingPullRequestAsync(PullRequestTrackingTicket pullRequestTrackingTicket)
        {
            var tableClient = GetTableClient();
            await tableClient.UpsertEntityAsync<PullRequestTrackingTicket>(pullRequestTrackingTicket, TableUpdateMode.Replace);
        }

        public async Task StopTrackingPullRequestAsync(PullRequestTrackingTicket pullRequestTrackingTicket)
        {
            var tableClient = GetTableClient();
            await tableClient.DeleteEntityAsync(pullRequestTrackingTicket.PartitionKey, pullRequestTrackingTicket.RowKey);
        }

        public async Task<IEnumerable<PullRequestTrackingTicket>> GetTrackedPullRequestsAsync()
        {
            var tableClient = GetTableClient();
            var pagable = tableClient.QueryAsync<PullRequestTrackingTicket>();
            var tickets = new List<PullRequestTrackingTicket>();

            await foreach (var ticket in pagable)
            {
                tickets.Add(ticket);
            }

            return tickets;
        }
    }
}
