using System;

namespace GitHubIssues.Reports
{
    internal partial class FindCustomerRelatedIssuesInvalidState
    {
        [Flags]
        private enum IssueType
        {
            None = 0,
            Bug = 1,
            Feature = 2,
            Question = 4,
        }
    }
}
