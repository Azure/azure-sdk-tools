using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Constants
{

    /// <summary>
    /// These are label constants that are common to all language repositories and ones
    /// that the rules use for processing. No team, or lanaguage specific labels belong
    /// in here.
    /// </summary>
    public class LabelConstants
    {
        public static readonly string CommunityContribution = "Community Contribution";
        public static readonly string CustomerReported = "customer-reported";
        public static readonly string CXPAttention = "CXP Attention";
        public static readonly string IssueAddressed = "issue-addressed";
        public static readonly string NeedsAuthorFeedback = "needs-author-feedback";
        public static readonly string NeedsTeamAttention = "needs-team-attention";
        public static readonly string NeedsTeamTriage = "needs-team-triage";
        public static readonly string NeedsTriage = "needs-triage";
        public static readonly string NoRecentActivity = "no-recent-activity";
        public static readonly string Question = "question";
        public static readonly string ServiceAttention = "Service Attention";
    }
}
