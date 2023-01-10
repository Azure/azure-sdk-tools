using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Octokit;

namespace Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload
{
    // In theory, we should be using deserializing the GitHubAction Payload Event into
    // Octokit's IssueEventPayload but it's missing the Label which we need for the
    // Labeled/Unlabeled Issue github action events.
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class IssueEventGitHubPayload : ActivityPayload
    {
        public string Action { get; private set; }
        public Issue Issue { get; private set; }
        public Label Label { get; private set; }
    }
}
