using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using NUnit.Framework;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Tests.Static
{
    // Note: All of the Scheduled Event tests will use the same payload. There's nothing in the
    // mocked payload that's necessary for processing.
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]

    public class ScheduledEventProcessingTests : ProcessingTestBase
    {
        /// <summary>
        /// Test the CloseAddressedIssues scheduled event.
        /// Each item returned from the query will have two updates:
        ///     Issue will be closed
        ///     Issue will have a comment added
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.CloseAddressedIssues, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.On)]
        [TestCase(RulesConstants.CloseAddressedIssues, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.Off)]
        public async Task TestCloseAddressedIssues(string rule, string payloadFile, RuleState ruleState)
        {
            int expectedUpdates = 100;
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            ScheduledEventGitHubPayload scheduledEventPayload = SimpleJsonSerializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
            // Create the fake issues to update. Because CloseAddressedIssues does 2 updates per issue
            // creating 100 results should only result in 50 issues being updated and 50 comments created
            mockGitHubEventClient.CreateSearchIssuesResult(expectedUpdates, scheduledEventPayload.Repository, ItemState.Open);
            await ScheduledEventProcessing.CloseAddressedIssues(mockGitHubEventClient, scheduledEventPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(scheduledEventPayload.Repository.Id);
            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                Assert.AreEqual(expectedUpdates, totalUpdates, $"The number of updates should have been {expectedUpdates} but was instead, {totalUpdates}");
                // There should be expectedUpdates/2 issueUpdates and expectedUpdates/2 comments
                int numIssueUpdates = mockGitHubEventClient.GetGitHubIssuesToUpdate().Count;
                Assert.AreEqual(expectedUpdates / 2, numIssueUpdates, $"The number of issue updates should have been {expectedUpdates / 2} but was instead, {numIssueUpdates}");
                int numComments = mockGitHubEventClient.GetComments().Count;
                Assert.AreEqual(expectedUpdates/2, numComments, $"The number of comments should have been {expectedUpdates / 2} but was instead, {numComments}");

                // The rule sets all of the Issue's States to ItemState.Closed. Only one needs to be verified
                Assert.AreEqual(mockGitHubEventClient.GetGitHubIssuesToUpdate()[0].IssueUpdate.State, ItemState.Closed, $"rule should have set all processed issues to {ItemState.Closed} and did not.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test the CloseStaleIssues scheduled event.
        /// Each item returned from the query will have one update:
        ///     Issue will be closed
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.CloseStaleIssues, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.On)]
        [TestCase(RulesConstants.CloseStaleIssues, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.Off)]
        public async Task TestCloseStaleIssues(string rule, string payloadFile, RuleState ruleState)
        {
            int expectedUpdates = 100;
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            ScheduledEventGitHubPayload scheduledEventPayload = SimpleJsonSerializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
            // Create the fake issues to update. 
            mockGitHubEventClient.CreateSearchIssuesResult(expectedUpdates, scheduledEventPayload.Repository, ItemState.Open);
            await ScheduledEventProcessing.CloseStaleIssues(mockGitHubEventClient, scheduledEventPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(scheduledEventPayload.Repository.Id);
            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                Assert.AreEqual(expectedUpdates, totalUpdates, $"The number of updates should have been {expectedUpdates} but was instead, {totalUpdates}");
                int numIssueUpdates = mockGitHubEventClient.GetGitHubIssuesToUpdate().Count;
                Assert.AreEqual(expectedUpdates, numIssueUpdates, $"The number of issue updates should have been {expectedUpdates} but was instead, {numIssueUpdates}");

                // The rule sets all of the Issue's States to ItemState.Closed. Only one needs to be verified
                Assert.AreEqual(mockGitHubEventClient.GetGitHubIssuesToUpdate()[0].IssueUpdate.State, ItemState.Closed, $"rule should have set all processed issues to {ItemState.Closed} and did not.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test the CloseStalePullRequests scheduled event.
        /// Each item returned from the query will have two updates:
        ///     PullRequest will be closed
        ///     PullRequest will have a comment added
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.CloseStalePullRequests, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.On)]
        [TestCase(RulesConstants.CloseStalePullRequests, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.Off)]
        public async Task TestCloseStalePullRequests(string rule, string payloadFile, RuleState ruleState)
        {
            int expectedUpdates = 100;
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            ScheduledEventGitHubPayload scheduledEventPayload = SimpleJsonSerializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
            // Create the fake issues to update. Because CloseStalePullRequests does 2 updates per issue
            // creating 100 results should only result in 50 issues being updated and 50 comments created
            mockGitHubEventClient.CreateSearchIssuesResult(expectedUpdates, scheduledEventPayload.Repository, ItemState.Open);
            await ScheduledEventProcessing.CloseStalePullRequests(mockGitHubEventClient, scheduledEventPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(scheduledEventPayload.Repository.Id);
            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                Assert.AreEqual(expectedUpdates, totalUpdates, $"The number of updates should have been {expectedUpdates} but was instead, {totalUpdates}");
                // There should be expectedUpdates/2 issueUpdates and expectedUpdates/2 comments
                int numIssueUpdates = mockGitHubEventClient.GetGitHubIssuesToUpdate().Count;
                Assert.AreEqual(expectedUpdates / 2, numIssueUpdates, $"The number of issue updates should have been {expectedUpdates / 2} but was instead, {numIssueUpdates}");
                int numComments = mockGitHubEventClient.GetComments().Count;
                Assert.AreEqual(expectedUpdates / 2, numComments, $"The number of comments should have been {expectedUpdates / 2} but was instead, {numComments}");

                // The rule sets all of the Issue's States to ItemState.Closed. Only one needs to be verified
                Assert.AreEqual(mockGitHubEventClient.GetGitHubIssuesToUpdate()[0].IssueUpdate.State, ItemState.Closed, $"rule should have set all processed pull requests to {ItemState.Closed} and did not.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test the IdentifyStalePullRequests scheduled event.
        /// Each item returned from the query will have two updates:
        ///     PullRequest will have NoRecentActivity label added
        ///     PullRequest will have a comment added
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.IdentifyStalePullRequests, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.On)]
        [TestCase(RulesConstants.IdentifyStalePullRequests, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.Off)]
        public async Task TestIdentifyStalePullRequests(string rule, string payloadFile, RuleState ruleState)
        {
            int expectedUpdates = 100;
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            ScheduledEventGitHubPayload scheduledEventPayload = SimpleJsonSerializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
            // Create the fake issues to update. Because IdentifyStalePullRequests does 2 updates per issue
            // creating 100 results should only result in 50 issues being updated and 50 comments created
            mockGitHubEventClient.CreateSearchIssuesResult(expectedUpdates, scheduledEventPayload.Repository, ItemState.Open);
            await ScheduledEventProcessing.IdentifyStalePullRequests(mockGitHubEventClient, scheduledEventPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(scheduledEventPayload.Repository.Id);
            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                Assert.AreEqual(expectedUpdates, totalUpdates, $"The number of updates should have been {expectedUpdates} but was instead, {totalUpdates}");
                // There should be expectedUpdates/2 issueUpdates and expectedUpdates/2 comments
                int numIssueUpdates = mockGitHubEventClient.GetGitHubIssuesToUpdate().Count;
                Assert.AreEqual(expectedUpdates / 2, numIssueUpdates, $"The number of issue updates should have been {expectedUpdates / 2} but was instead, {numIssueUpdates}");
                int numComments = mockGitHubEventClient.GetComments().Count;
                Assert.AreEqual(expectedUpdates / 2, numComments, $"The number of comments should have been {expectedUpdates / 2} but was instead, {numComments}");

                // Verify LabelConstants.NoRecentActivity was added
                Assert.IsTrue(mockGitHubEventClient.GetGitHubIssuesToUpdate()[0].IssueUpdate.Labels.Contains(LabelConstants.NoRecentActivity), $"rule should have added {LabelConstants.NoRecentActivity} label and did not.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test the IdentifyStaleIssues scheduled event.
        /// Each item returned from the query will have two updates:
        ///     Issue will have NoRecentActivity label added
        ///     Issue will have a comment added
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.IdentifyStaleIssues, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.On)]
        [TestCase(RulesConstants.IdentifyStaleIssues, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.Off)]
        public async Task TestIdentifyStaleIssues(string rule, string payloadFile, RuleState ruleState)
        {
            int expectedUpdates = 100;
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            ScheduledEventGitHubPayload scheduledEventPayload = SimpleJsonSerializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
            // Create the fake issues to update. Because IdentifyStaleIssues does 2 updates per issue
            // creating 100 results should only result in 50 issues being updated and 50 comments created
            mockGitHubEventClient.CreateSearchIssuesResult(expectedUpdates, scheduledEventPayload.Repository, ItemState.Open);
            await ScheduledEventProcessing.IdentifyStaleIssues(mockGitHubEventClient, scheduledEventPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(scheduledEventPayload.Repository.Id);
            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                Assert.AreEqual(expectedUpdates, totalUpdates, $"The number of updates should have been {expectedUpdates} but was instead, {totalUpdates}");
                // There should be expectedUpdates/2 issueUpdates and expectedUpdates/2 comments
                int numIssueUpdates = mockGitHubEventClient.GetGitHubIssuesToUpdate().Count;
                Assert.AreEqual(expectedUpdates / 2, numIssueUpdates, $"The number of issue updates should have been {expectedUpdates / 2} but was instead, {numIssueUpdates}");
                int numComments = mockGitHubEventClient.GetComments().Count;
                Assert.AreEqual(expectedUpdates / 2, numComments, $"The number of comments should have been {expectedUpdates / 2} but was instead, {numComments}");

                // Verify LabelConstants.NoRecentActivity was added
                Assert.IsTrue(mockGitHubEventClient.GetGitHubIssuesToUpdate()[0].IssueUpdate.Labels.Contains(LabelConstants.NoRecentActivity), $"rule should have added {LabelConstants.NoRecentActivity} label and did not.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test the LockClosedIssues scheduled event.
        /// Each item returned from the query will have one update:
        ///     Issue will be locked
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.LockClosedIssues, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.On)]
        [TestCase(RulesConstants.LockClosedIssues, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.Off)]
        public async Task TestLockClosedIssues(string rule, string payloadFile, RuleState ruleState)
        {
            int expectedUpdates = 100;
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            ScheduledEventGitHubPayload scheduledEventPayload = SimpleJsonSerializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
            // Create the fake issues to update. 
            mockGitHubEventClient.CreateSearchIssuesResult(expectedUpdates, scheduledEventPayload.Repository, ItemState.Open);
            await ScheduledEventProcessing.LockClosedIssues(mockGitHubEventClient, scheduledEventPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(scheduledEventPayload.Repository.Id);
            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                Assert.AreEqual(expectedUpdates, totalUpdates, $"The number of updates should have been {expectedUpdates} but was instead, {totalUpdates}");
                int numIssuesToLock = mockGitHubEventClient.GetGitHubIssuesToLock().Count;
                Assert.AreEqual(expectedUpdates, numIssuesToLock, $"The number of issues to lock should have been {expectedUpdates} but was instead, {numIssuesToLock}");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }
    }
}
