using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using Octokit;

namespace Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload
{
    /// <summary>
    /// Class used to deserialize PullRequest event payloads. The reason why Octokit's PullRequestEventPayload
    /// can't be used directly is because it's missing the Label which necessary to know the label added/removed
    /// for the labeled/unlabeled events. This class uses the existing Octokit classes as well as Octokit's
    /// SimpleJsonSerializer for deserialization. The other anomaly here is the AutoMergeEnabled which requires
    /// special processing to set.
    /// </summary>
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
