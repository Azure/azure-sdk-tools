using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Services.PullRequestTracking
{
    public class PullRequestTracker : IPullRequestTracker
    {
        private List<PullRequestTrackingTicket> pullRequestTrackingTickets = new List<PullRequestTrackingTicket>();

        public PullRequestTracker()
        {
        }

        public async Task StartTrackingPullRequestAsync(PullRequestTrackingTicket pullRequestTrackingTicket)
        {
            pullRequestTrackingTickets.Add(pullRequestTrackingTicket);
        }

        public async Task StopTrackingPullRequestAsync(PullRequestTrackingTicket pullRequestTrackingTicket)
        {
            pullRequestTrackingTickets.RemoveAll((ticket) =>
            {
                return
                    ticket.InstallationId == pullRequestTrackingTicket.InstallationId &&
                    ticket.RepositoryId == pullRequestTrackingTicket.RepositoryId &&
                    ticket.PullRequestNumber == pullRequestTrackingTicket.PullRequestNumber;
            });
        }

        public async Task<IEnumerable<PullRequestTrackingTicket>> GetTrackedPullRequestsAsync()
        {
            return pullRequestTrackingTickets.ToImmutableList();
        }
    }
}
