using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Constants
{

    /// <summary>
    /// These are triage label constants and are common to all language repositories. No category labels,
    /// service labels or repository specific labels belong in here.
    /// </summary>
    public class TriageLabelConstants
    {
        public const string CommunityContribution = "Community Contribution";
        public const string CustomerReported = "customer-reported";
        public const string IssueAddressed = "issue-addressed";
        public const string NeedsAuthorFeedback = "needs-author-feedback";
        public const string NeedsTeamAttention = "needs-team-attention";
        public const string NeedsTeamTriage = "needs-team-triage";
        public const string NeedsTriage = "needs-triage";
        public const string NoRecentActivity = "no-recent-activity";
        public const string Question = "question";
        public const string ServiceAttention = "Service Attention";
        public const string AutoCloseExempt = "auto-close-exempt";
    }
}
