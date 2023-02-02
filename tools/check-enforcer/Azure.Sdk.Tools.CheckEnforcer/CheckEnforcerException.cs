using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{

    [Serializable]
    public class CheckEnforcerException : Exception
    {
        public CheckEnforcerException(string message) : base(message) { }
        public CheckEnforcerException(string message, Exception inner) : base(message, inner) { }
        protected CheckEnforcerException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
