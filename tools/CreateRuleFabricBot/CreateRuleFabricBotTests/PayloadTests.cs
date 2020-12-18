using CreateRuleFabricBot;
using CreateRuleFabricBot.Rules.IssueRouting;
using CreateRuleFabricBot.Rules.PullRequestLabel;
using CreateRuleFabricBot.Service;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using System;
using System.Collections.Generic;
using System.Linq;
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
  ""id"": ""###taskId###""
}";

            Assert.AreEqual(expectedPayload, irc.GetPayload());
        }


        [Test]
        public void ValidatePullRequestRouting()
        {
            PullRequestLabelFolderCapability irc = new PullRequestLabelFolderCapability("test", "repo", null);
            irc.AddEntry("/test", "Label1");

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

            Assert.AreEqual(expectedPayload, irc.GetPayload());
        }
    }
}