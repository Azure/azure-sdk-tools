using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Octokit;

namespace Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload
{
    /// <summary>
    /// Class used to deserialize Issue event payloads. The reason why Octokit's IssueEventPayload
    /// can't be used directly is because it's missing the Label which necessary to know the label
    /// added/removed for the labeled/unlabeled events. This class uses the existing Octokit classes
    /// as well as Octokit's SimpleJsonSerializer for deserialization.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class IssueEventGitHubPayload : ActivityPayload
    {
        public string Action { get; private set; }
        public Issue Issue { get; private set; }
        public Label Label { get; private set; }
    }
}
