using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using Octokit;

namespace Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload
{
    // In theory, we should be using deserializing the GitHubAction Payload Event into
    // Octokit's PullRequestEventPayload but it's missing the Label which we need for the
    // Labeled/Unlabeled PullRequest github action events.
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class PullRequestEventGitHubPayload : ActivityPayload
    {
        public string Action { get; private set; }
        public int Number { get; private set; }
        public PullRequest PullRequest { get; private set; }
        public Label Label { get; private set; }

        // The actions event payload for a pull_request has a class on the pull request that
        // the OctoKit.PullRequest class does not have. If the user has enabled Auto-Merge
        // through the pull request UI.
        public bool AutoMergeEnabled { get; set; }
        
    }
}
