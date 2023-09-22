using System;
namespace Azure.Sdk.Tools.TestProxy.Common.Exceptions
{
    public class SessionNotActiveException: Exception
    {
        public SessionNotActiveException()
        {
        }
        public SessionNotActiveException(string message)
        : base(message)
        {
        }
    }
}
