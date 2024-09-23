using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Octokit;

namespace Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload
{

    /// <summary>
    /// Class used to deserialize Scheduled event payloads. There is no Octokit equivalent
    /// class for Scheduled events. This class uses the existing Octokit Repository classes
    /// as well as Octokit's SimpleJsonSerializer for deserialization.
    /// </summary>
    public class ScheduledEventGitHubPayload
    {
        public Repository Repository { get; protected set; }
        public string Schedule { get; private set; }
        public string Workflow { get; private set; }
    }

}
