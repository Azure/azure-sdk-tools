using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{

    [Serializable]
    public class CheckEnforcerUnsupportedEventException : CheckEnforcerException
    {
        public CheckEnforcerUnsupportedEventException(string eventName) : base($"The GitHub event '{eventName}' cannot be processed.") { }
        public CheckEnforcerUnsupportedEventException(string eventName, Exception inner) : base($"The GitHub event '{eventName}' does not have a handler.", inner) { }
        protected CheckEnforcerUnsupportedEventException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
