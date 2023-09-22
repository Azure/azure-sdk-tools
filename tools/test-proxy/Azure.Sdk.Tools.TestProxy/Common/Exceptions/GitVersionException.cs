using System;
namespace Azure.Sdk.Tools.TestProxy.Common.Exceptions
{
    public class GitVersionException: Exception
    {
        public GitVersionException()
        {
        }
        public GitVersionException(string message)
        : base(message)
        {
        }
    }
}
