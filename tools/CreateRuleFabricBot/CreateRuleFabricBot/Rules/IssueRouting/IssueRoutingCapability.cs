using System.Collections.Generic;

namespace CreateRuleFabricBot.Rules.IssueRouting
{
    public class IssueRoutingCapability
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
  ""id"": ""###repo###""
}";

        private readonly string _repo;
        private readonly string _owner;
        private List<TriageConfig> _triageConfig = new List<TriageConfig>();

        public int RouteCount { get { return _triageConfig.Count; } }

        public IssueRoutingCapability(string owner, string repo)
        {
            _repo = repo;
            _owner = owner;
        }

        public void AddService(IEnumerable<string> labels, IEnumerable<string> mentionees)
        {
            var tc = new TriageConfig();
            tc.Labels.AddRange(labels);
            tc.Mentionee.AddRange(mentionees);
            _triageConfig.Add(tc);
        }

        public static string GetTaskId(string owner, string repo)
        {
            return $"AzureSDKTriage_{owner}_{repo}";
        }

        public string ToJson()
        {
            return s_template
                .Replace("###repo###", GetTaskId(_owner, _repo))
                .Replace("###labelsAndMentions###", string.Join(",", _triageConfig));
        }
    }
}
