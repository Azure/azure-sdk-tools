using CreateRuleFabricBot;
using CreateRuleFabricBot.Rules.IssueRouting;
using CreateRuleFabricBot.Rules.PullRequestLabel;
using CreateRuleFabricBot.Service;
using CreateRuleFabricBotTests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Tests
{
    public class PayloadTests
    {
        [Test]
        public void ValidateIssueRouting()
        {
            IssueRoutingCapability irc = new IssueRoutingCapability("test", "repo", null);
            irc.AddRoute(new string[] { "label1", "label2" }, new string[] { "user1", "user2" });

            string expectedPayload = @"
{
  ""taskType"" : ""scheduledAndTrigger"",
  ""capabilityId"" : ""IssueRouting"",
  ""version"" : ""1.0"",
  ""subCapability"": ""@Mention"",
  ""config"": { 
    ""labelsAndMentions"": [
        { ""labels"": [  ""label1"",""label2""  ], ""mentionees"": [ ""user1"",""user2""  ] }
    ],
    ""replyTemplate"": ""Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc ${mentionees}."",
    ""taskName"" :""Triage issues to the service team""
  },
  ""id"": ""AzureSDKTriage_test_repo""
}";

            AreJsonEquivalent(expectedPayload, irc.GetPayload());
        }


        [Test]
        public void ValidatePullRequestRouting()
        {
            PullRequestLabelFolderCapability plfc = new PullRequestLabelFolderCapability("test", "repo", null);
            plfc.AddEntry("/test", "Label1");

            string expectedPayload = @"
{
      ""taskType"": ""trigger"",
      ""capabilityId"": ""PrAutoLabel"",
      ""subCapability"": ""Path"",
      ""version"": ""1.0"",
      ""id"": ""AzureSDKPullRequestLabelFolder_test_repo"",
      ""config"": {
        ""configs"": [
            { ""labels"": [""Label1""], ""pathFilter"": [""test""], ""exclude"": [ """" ]  }
        ],
      ""taskName"" :""Auto PR based on folder paths ""
      }
    }
";

            AreJsonEquivalent(expectedPayload, plfc.GetPayload());
        }

        [Test]
        public void ValidatePathConfigPayload()
        {
            PathConfig pc = new PathConfig("folder", "path");

            string expectedPayload = @"{
  ""labels"": [
    ""path""
  ],
  ""pathFilter"": [ ""folder"" ], ""exclude"": [ """" ]
}";

            AreJsonEquivalent(expectedPayload, pc.ToString());
        }

        [Test]
        public void ValidateTriageConfigPayload()
        {
            TriageConfig tc = new TriageConfig();
            tc.Labels.Add("Label1");
            tc.Labels.Add("Label2");
            tc.Mentionee.Add("User1");
            tc.Mentionee.Add("User2");

            string expectedPayload = @"{
                ""labels"": [  ""Label1"",""Label2""  ], 
                ""mentionees"": [ ""User1"",""User2""  ]}";

            AreJsonEquivalent(expectedPayload, tc.ToString());
        }

        private void AreJsonEquivalent(string expected, string actual)
        {
            expected = JObject.Parse(expected).ToString(Formatting.Indented);
            actual = JObject.Parse(actual).ToString(Formatting.Indented);

            Assert.AreEqual(expected, actual);
        }
    }
}