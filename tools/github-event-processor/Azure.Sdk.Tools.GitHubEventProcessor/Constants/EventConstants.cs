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
        public const string issues = "issues";
        public const string issue_comment = "issue_comment";
        public const string pull_request_review = "pull_request_review";
        public const string pull_request_target = "pull_request_target";
        public const string schedule = "schedule";
    }
}
