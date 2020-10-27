using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Services.PullRequestTracking
{
    public interface IPullRequestTracker
    {
        Task StartTrackingPullRequestAsync(PullRequestTrackingTicket pullRequest);
        Task StopTrackingPullRequestAsync(PullRequestTrackingTicket pullRequest);
        Task<IEnumerable<PullRequestTrackingTicket>> GetTrackedPullRequestsAsync();
    }
}
