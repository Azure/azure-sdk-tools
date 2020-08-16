using CreateRuleFabricBot.CommandLine;
using CreateRuleFabricBot.Markdown;
using OutputColorizer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CreateRuleFabricBot.Rules.IssueRouting
{
    public class IssueRoutingCapability : BaseCapability
    {
        private static readonly string s_template = @"
{
  ""taskType"" : ""scheduledAndTrigger"",
  ""capabilityId"" : ""IssueRouting"",
  ""version"" : ""1.0"",
  ""subCapability"": ""@Mention"",
  ""config"": { 
    ""labelsAndMentions"": [
        ###labelsAndMentions###
    ],
    ""replyTemplate"": ""Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc ${mentionees}."",
    ""taskName"" :""Triage issues to the service team""
  },
  ""id"": ""###taskId###""
}";

        private readonly string _repo;
        private readonly string _owner;
        private readonly string _servicesFile;

        private List<TriageConfig> _triageConfig = new List<TriageConfig>();

        private int RouteCount { get { return _triageConfig.Count; } }

        public IssueRoutingCapability(string org, string repo, string servicesFile)
        {
            _repo = repo;
            _owner = org;
            _servicesFile = servicesFile;
        }
        private void AddService(IEnumerable<string> labels, IEnumerable<string> mentionees)
        {
            var tc = new TriageConfig();
            tc.Labels.AddRange(labels);
            tc.Mentionee.AddRange(mentionees);
            _triageConfig.Add(tc);
        }

        public override string GetPayload()
        {
            Colorizer.Write("Parsing service table... ");
            MarkdownTable table = MarkdownTable.Parse(_servicesFile);
            Colorizer.WriteLine("[Green!done].");

            foreach (var row in table.Rows)
            {
                if (!string.IsNullOrEmpty(row[2].Trim()))
                {
                    // the row at position 0 is the label to use on top of 'Service Attention'
                    string[] labels = new string[] { "Service Attention", row[0] };

                    // The row at position 2 is the set of mentionees to ping on the issue.
                    IEnumerable<string> mentionees = row[2].Split(',').Select(x => x.Replace("@", "").Trim());

                    //add the service
                    AddService(labels, mentionees);
                }
            }

            Colorizer.WriteLine("Found [Yellow!{0}] service routes.", RouteCount);

            return s_template
                .Replace("###repo###", GetTaskId())
                .Replace("###labelsAndMentions###", string.Join(",", _triageConfig));
        }

        public override string GetTaskId()
        {
            return $"AzureSDKTriage_{_owner}_{_repo}";
        }
    }
}
