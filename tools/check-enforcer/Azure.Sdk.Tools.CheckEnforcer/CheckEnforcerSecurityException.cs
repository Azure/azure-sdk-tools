using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{

    [Serializable]
    public class CheckEnforcerSecurityException : CheckEnforcerException
    {
        public CheckEnforcerSecurityException(string message) : base(message) { }
        public CheckEnforcerSecurityException(string message, Exception inner) : base(message, inner) { }
        protected CheckEnforcerSecurityException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
