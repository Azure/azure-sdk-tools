using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    [Serializable]
    public class RouterAuthorizationException : RouterException
    {
        public RouterAuthorizationException() { }
        public RouterAuthorizationException(string message) : base(message) { }
        public RouterAuthorizationException(string message, Exception inner) : base(message, inner) { }
        protected RouterAuthorizationException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
