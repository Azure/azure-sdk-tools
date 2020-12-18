using CreateRuleFabricBot.CommandLine;
using CreateRuleFabricBot.Markdown;
using OutputColorizer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

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

        private List<TriageConfig> _triageConfig = new List<TriageConfig>();

        private int RouteCount { get { return _triageConfig.Count; } }

        public IssueRoutingCapability(string org, string repo, string configurationFile)
            : base(org, repo, configurationFile)
        {
        }

        public void AddRoute(IEnumerable<string> labels, IEnumerable<string> mentionees)
        {
            TriageConfig tc = new TriageConfig();
            tc.Labels.AddRange(labels);
            tc.Mentionee.AddRange(mentionees);
            _triageConfig.Add(tc);
        }

        public override string GetPayload()
        {
            Colorizer.WriteLine("Found [Yellow!{0}] service routes.", RouteCount);
            foreach (TriageConfig triage in _triageConfig)
            {
                Colorizer.WriteLine("Labels:[Yellow!{0}], Owners:[Yellow!{1}]", string.Join(',', triage.Labels), string.Join(',', triage.Mentionee));
            }

            return s_template
                .Replace("###repo###", GetTaskId())
                .Replace("###labelsAndMentions###", string.Join(",", _triageConfig));
        }

        public override string GetTaskId()
        {
            return $"AzureSDKTriage_{_owner}_{_repo}";
        }

        internal override void ReadConfigurationFromFile(string configurationFile)
        {
            List<CodeOwnerEntry> entries = CodeOwnersFile.ParseFile(configurationFile);

            foreach (CodeOwnerEntry entry in entries)
            {
                // If we have labels for the specific codeowners entry, add that to the triage list
                if (entry.ServiceLabels.Any())
                {
                    // Remove the '@' from the owners handle
                    IEnumerable<string> mentionees = entry.Owners.Select(x => x.Replace("@", "").Trim());

                    //add the service
                    AddRoute(entry.ServiceLabels, mentionees);
                }
            }
        }
    }
}
