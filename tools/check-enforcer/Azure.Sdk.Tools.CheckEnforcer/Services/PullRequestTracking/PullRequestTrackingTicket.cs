using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer.Services.PullRequestTracking
{
    public class PullRequestTrackingTicket
    {
        public long InstallationId { get; set; }
        public long RepositoryId { get; set; }
        public int PullRequestNumber { get; set;  }
    }
}
