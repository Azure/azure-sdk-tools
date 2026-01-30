using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Constants
{
    public class McpConstants
    {
        public const string McpServerLabelPrefix = "server-"; 
        public const string McpToolLabelPrefix = "tools-";
        public static readonly List<string> McpOtherLabels = new List<string> { "remote-mcp" };
        public const string McpConfigPrefix = "microsoft/mcp";
    }
}
