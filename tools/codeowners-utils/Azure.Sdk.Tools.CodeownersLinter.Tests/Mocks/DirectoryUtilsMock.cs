using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Mocks
{
    /// <summary>
    /// Mock DirectoryUtils class. This is necessary for NUnit tests which need to be able to run
    /// without actually having a repository directory.
    /// </summary>
    public class DirectoryUtilsMock: DirectoryUtils
    {
        public DirectoryUtilsMock() 
        {
        }

        // This is the only method that needs to be overridden to call nothing else and then return;
        public override void VerifySourcePathEntry(string sourcePathEntry, List<string> errorStrings)
        {
            return;
        }
    }
}
