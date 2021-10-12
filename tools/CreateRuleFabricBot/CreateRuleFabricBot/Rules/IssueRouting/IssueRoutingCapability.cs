using Azure.Sdk.Tools.CodeOwnersParser;
using Newtonsoft.Json.Linq;
using OutputColorizer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CreateRuleFabricBot.Rules.IssueRouting
{
    public class IssueRoutingCapability : BaseCapability
    {
        private readonly List<TriageConfig> _triageConfig = new List<TriageConfig>();

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

            // create the payload
            JObject payload = new JObject(
                new JProperty("taskType", "scheduledAndTrigger"),
                new JProperty("capabilityId", "IssueRouting"),
                new JProperty("version", "1.0"),
                new JProperty("subCapability", "@Mention"),
                new JProperty("config",
                    new JObject(
                        new JProperty("labelsAndMentions", new JArray(_triageConfig.Select(tc => tc.GetJsonPayload()))),
                        new JProperty("replyTemplate", "Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc ${mentionees}."),
                        new JProperty("taskName", "Triage issues to the service team")
                    )
                ),
                new JProperty("id", new JValue(GetTaskId())));

            return payload.ToString();
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
