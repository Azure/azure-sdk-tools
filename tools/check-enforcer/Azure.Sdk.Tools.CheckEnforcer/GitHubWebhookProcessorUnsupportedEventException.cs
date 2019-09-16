using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{

    [Serializable]
    public class GitHubWebhookProcessorUnsupportedEventException : GitHubWebhookProcessorException
    {
        public GitHubWebhookProcessorUnsupportedEventException(string eventName) : base($"The GitHub event '{eventName}' cannot be processed.") { }
        public GitHubWebhookProcessorUnsupportedEventException(string eventName, Exception inner) : base($"The GitHub event '{eventName}' does not have a handler.", inner) { }
        protected GitHubWebhookProcessorUnsupportedEventException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
