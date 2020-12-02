using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    [Serializable]
    public class RouterConfigurationException : RouterException
    {
        public RouterConfigurationException() { }
        public RouterConfigurationException(string message) : base(message) { }
        public RouterConfigurationException(string message, Exception inner) : base(message, inner) { }
        protected RouterConfigurationException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
