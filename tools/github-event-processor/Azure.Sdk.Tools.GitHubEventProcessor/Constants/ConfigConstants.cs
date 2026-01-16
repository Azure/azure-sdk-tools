using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Constants
{
    public class ConfigConstants
    {
        public const string RawGitHubUserContentUrl = "https://raw.githubusercontent.com";
        public const string DefaultBranch = "main";
        public const string ServerLabelPrefix = "server-"; 
        public const string ToolLabelPrefix = "tools-";
    }
}
