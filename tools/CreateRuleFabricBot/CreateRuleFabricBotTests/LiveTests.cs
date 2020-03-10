using CreateRuleFabricBot.Rules.IssueRouting;
using CreateRuleFabricBot.Service;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;

namespace Tests
{
    public class Tests
    {
        private string _org = "<PleaseSpecify>";
        private string _repo = "<PleaseSpecify>";
        private FabricBotClient _requestSender;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            string token = Environment.GetEnvironmentVariable("officebot_token");
            if (token == null)
            {
                throw new InvalidOperationException("Authentication token not found.");
            }
            _requestSender = new FabricBotClient(_org, _repo, token);
        }

        [Test]
        public void CreateUpdateDeleteRule()
        {
            IssueRoutingCapability irc = new IssueRoutingCapability(_org, _repo);

            _requestSender.CreateTask(irc.ToJson());
            // delay for the service to react
            Thread.Sleep(3000);

            var ids = _requestSender.GetTaskIds();
            Assert.Contains(IssueRoutingCapability.GetTaskId(_org, _repo), ids);
            // delay for the service to react
            Thread.Sleep(3000);

            _requestSender.UpdateTask(IssueRoutingCapability.GetTaskId(_org, _repo), irc.ToJson());
            // delay for the service to react
            Thread.Sleep(3000);

            ids = _requestSender.GetTaskIds();
            Assert.Contains(IssueRoutingCapability.GetTaskId(_org, _repo), ids);
            // delay for the service to react
            Thread.Sleep(3000);

            _requestSender.DeleteTask(IssueRoutingCapability.GetTaskId(_org, _repo));
            // delay for the service to react
            Thread.Sleep(3000);

            ids = _requestSender.GetTaskIds();
            Assert.False(ids.Any(x => x == IssueRoutingCapability.GetTaskId(_org, _repo)));
        }
    }
}