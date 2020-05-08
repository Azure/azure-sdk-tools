using System;

namespace GitHubIssues.Reports
{
    internal partial class FindCustomerRelatedIssuesInvalidState
    {
        [Flags]
        private enum ImpactArea
        {
            None = 0,
            Client = 1,
            Mgmt = 2,
            Service = 4,
            EngSys = 8,
            MgmtEngSys = 16
        }
    }
}
