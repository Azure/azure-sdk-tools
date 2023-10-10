using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Constants
{
    /// <summary>
    /// Rules constants are used in the rules configuration to determine which actions are
    /// available for a given repository.
    /// </summary>
    public class RulesConstants
    {
        // issue rules
        public const string InitialIssueTriage = "InitialIssueTriage";
        public const string ManualIssueTriage = "ManualIssueTriage";
        public const string ServiceAttention = "ServiceAttention";
        public const string ManualTriageAfterExternalAssignment = "ManualTriageAfterExternalAssignment";
        public const string RequireAttentionForNonMilestone = "RequireAttentionForNonMilestone";
        public const string AuthorFeedbackNeeded = "AuthorFeedbackNeeded";
        public const string IssueAddressed = "IssueAddressed";
        public const string IssueAddressedReset = "IssueAddressedReset";

        // issue_comment rules
        public const string AuthorFeedback = "AuthorFeedback";
        public const string ReopenIssue = "ReopenIssue";
        public const string DeclineToReopenIssue = "DeclineToReopenIssue";
        public const string IssueAddressedCommands = "IssueAddressedCommands";

        // pull_request rules
        public const string PullRequestTriage = "PullRequestTriage";
        public const string ResetApprovalsForUntrustedChanges = "ResetApprovalsForUntrustedChanges";

        // pull_request_comment rules
        public const string ReopenPullRequest = "ReopenPullRequest";

        // Rules that apply to multiple events
        // ResetIssueActivity applies to issue and issue_comment
        public const string ResetIssueActivity = "ResetIssueActivity";
        // ResetPullRequestActivity applies to pull_request, pull_request_comment and pull_request_review
        public const string ResetPullRequestActivity = "ResetPullRequestActivity";

        // Cron task rules
        public const string CloseStaleIssues = "CloseStaleIssues";
        public const string CloseStalePullRequests = "CloseStalePullRequests";
        public const string IdentifyStaleIssues = "IdentifyStaleIssues";
        public const string IdentifyStalePullRequests = "IdentifyStalePullRequests";
        public const string CloseAddressedIssues = "CloseAddressedIssues";
        public const string LockClosedIssues = "LockClosedIssues";

    }
}
