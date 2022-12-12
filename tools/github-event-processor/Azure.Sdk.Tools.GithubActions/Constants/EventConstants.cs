using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.GithubEventProcessor.Constants
{
    // These should be static readonly but need to be const because of the switch
    public class EventConstants
    {
        public const string issue = "issue";
        public const string issue_comment = "issue_comment";
        public const string pull_request_target = "pull_request_target";
        public const string pull_request_review = "pull_request_review";
    }
}
