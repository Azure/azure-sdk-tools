using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Octokit;

namespace Azure.Sdk.Tools.GithubEventProcessor.GitHubPayload
{

    public class ScheduledEventGitHubPayload
    {
        public Repository Repository { get; protected set; }
        public string Schedule { get; private set; }
        public string Workflow { get; private set; }
    }

}
