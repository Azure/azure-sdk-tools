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

        //[OneTimeSetUp]
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
        [Ignore("Can't run test without officebot_token")]
        public void CreateUpdateDeleteRule()
        {
            IssueRoutingCapability irc = new IssueRoutingCapability(_org, _repo, "");

            _requestSender.CreateTask(irc.GetPayload());
            // delay for the service to react
            Thread.Sleep(3000);

            var ids = _requestSender.GetTaskIds();
            Assert.Contains(irc.GetTaskId(), ids);
            // delay for the service to react
            Thread.Sleep(3000);

            _requestSender.UpdateTask(irc.GetTaskId(), irc.GetPayload());
            // delay for the service to react
            Thread.Sleep(3000);

            ids = _requestSender.GetTaskIds();
            Assert.Contains(irc.GetTaskId(), ids);
            // delay for the service to react
            Thread.Sleep(3000);

            _requestSender.DeleteTask(irc.GetTaskId());
            // delay for the service to react
            Thread.Sleep(3000);

            ids = _requestSender.GetTaskIds();
            Assert.False(ids.Any(x => x == irc.GetTaskId()));
        }
    }
}