using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{

    [Serializable]
    public class CheckEnforcerConfigurationException : Exception
    {
        public CheckEnforcerConfigurationException(string message) : base(message) { }
        public CheckEnforcerConfigurationException(string message, Exception inner) : base(message, inner) { }
        protected CheckEnforcerConfigurationException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
