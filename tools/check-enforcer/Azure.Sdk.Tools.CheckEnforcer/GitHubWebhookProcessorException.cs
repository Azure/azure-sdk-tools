using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{

    [Serializable]
    public class GitHubWebhookProcessorException : Exception
    {
        public GitHubWebhookProcessorException(string message) : base(message) { }
        public GitHubWebhookProcessorException(string message, Exception inner) : base(message, inner) { }
        protected GitHubWebhookProcessorException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
