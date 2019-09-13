using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{

    [Serializable]
    public class GitHubWebhookHandlerNotRegisteredException : GitHubWebhookProcessorException
    {
        public GitHubWebhookHandlerNotRegisteredException(string eventName) : base($"The GitHub event '{eventName}' does not have a handler.") { }
        public GitHubWebhookHandlerNotRegisteredException(string eventName, Exception inner) : base($"The GitHub event '{eventName}' does not have a handler.", inner) { }
        protected GitHubWebhookHandlerNotRegisteredException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
