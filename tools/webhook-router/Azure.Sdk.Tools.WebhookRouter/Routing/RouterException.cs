using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    [Serializable]
    public class RouterException : Exception
    {
        public RouterException() { }
        public RouterException(string message) : base(message) { }
        public RouterException(string message, Exception inner) : base(message, inner) { }
        protected RouterException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
