using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class GitHubWebhookEvent
    {
        public GitHubWebhookEvent(string eventName, string json)
        {
            EventName = eventName;
            Json = json;
        }

        public string EventName { get; private set; }
        public string Json { get; private set; }
    }
}
