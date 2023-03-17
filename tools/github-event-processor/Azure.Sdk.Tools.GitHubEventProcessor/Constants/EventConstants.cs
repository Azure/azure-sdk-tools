using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Constants
{

    /// <summary>
    /// Event constants for the events that are being being processed. These are from the
    /// github docs https://docs.github.com/en/actions/using-workflows/events-that-trigger-workflows.
    /// </summary>
    public class EventConstants
    {
        public const string Issues = "issues";
        public const string IssueComment = "issue_comment";
        public const string PullRequestReview = "pull_request_review";
        public const string PullRequestTarget = "pull_request_target";
        public const string Schedule = "schedule";
    }
}
