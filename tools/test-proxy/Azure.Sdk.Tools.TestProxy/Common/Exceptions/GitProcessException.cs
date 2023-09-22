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

        // Override the ToString so it'll give the command exception's toString which
        // will contain the accurate message and callstack. This is necessary in the
        // event the exception goes unhandled.
        public override string ToString()
        {
            return $"GitProcessException: {Result.CommandException}";
        }
    }
}
