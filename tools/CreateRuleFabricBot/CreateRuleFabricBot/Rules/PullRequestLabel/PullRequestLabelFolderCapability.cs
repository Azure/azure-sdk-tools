using CreateRuleFabricBot.CommandLine;
using CreateRuleFabricBot.Markdown;
using OutputColorizer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CreateRuleFabricBot.Rules.IssueRouting
{
    public class PullRequestLabelFolderCapability : BaseCapability
    {
        private static readonly string s_template = @"
{
      ""taskType"": ""trigger"",
      ""capabilityId"": ""PrAutoLabel"",
      ""subCapability"": ""Path"",
      ""version"": ""1.0"",
      ""id"": ""###taskId###"",
      ""config"": {
        ""configs"": [
          {
            ""label"": ""###label###"",
            ""pathFilter"": [ ###srcFolders### ],
            ""exclude"": [ ]
    }
        ]
      }
    }
";

        private readonly string _repo;
        private readonly string _owner;
        private readonly string _codeownersFile;

        public PullRequestLabelFolderCapability(string org, string name, string codeownersFile)
        {
            _repo = org;
            _owner = name;
            _codeownersFile = codeownersFile;
        }

        public override string GetPayload()
        {
            Colorizer.Write("Parsing CODEOWNERS table... ");

            return s_template
                .Replace("###taskId###", GetTaskId())
                .Replace("###label###", "Client")
                .Replace(" ###srcFolders###", string.Join(",", new string[] { "src1", "src2" }));
        }

        public override string GetTaskId()
        {
            return $"AzureSDKPullRequestLabelFolder_{_owner}_{_repo}";
        }
    }
}
