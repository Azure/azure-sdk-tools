using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Store;

namespace Azure.Sdk.Tools.TestProxy.Common.Exceptions
{
    public class GitProcessException : Exception
    {
        public CommandResult Result { get; }

        public GitProcessException(CommandResult result)
        {
            Result = result;
        }
    }
}
