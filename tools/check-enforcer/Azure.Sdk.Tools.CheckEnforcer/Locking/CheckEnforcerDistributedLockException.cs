using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer.Locking
{

    [Serializable]
    public class CheckEnforcerDistributedLockException : Exception
    {
        public CheckEnforcerDistributedLockException(string message) : base(message) { }
        public CheckEnforcerDistributedLockException(string message, Exception inner) : base(message, inner) { }
        protected CheckEnforcerDistributedLockException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
