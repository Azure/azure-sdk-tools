using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Octokit;

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
            var scheduledEventProcessing = CreateScheduledEventProcessingInstance();
            await scheduledEventProcessing.CloseAddressedIssues(mockGitHubEventClient, scheduledEventPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingScheduledUpdates();
            // Verify the RuleCheck 
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                Assert.That(totalUpdates, Is.EqualTo(expectedUpdates), $"The number of updates should have been {expectedUpdates} but was instead, {totalUpdates}");
                // There should be expectedUpdates/2 issueUpdates and expectedUpdates/2 comments
                int numIssueUpdates = mockGitHubEventClient.GetGitHubIssuesToUpdate().Count;
                Assert.That(numIssueUpdates, Is.EqualTo(expectedUpdates / 2), $"The number of issue updates should have been {expectedUpdates / 2} but was instead, {numIssueUpdates}");
                int numComments = mockGitHubEventClient.GetComments().Count;
                Assert.That(numComments, Is.EqualTo(expectedUpdates / 2), $"The number of comments should have been {expectedUpdates / 2} but was instead, {numComments}");

                // The rule sets all of the Issue's States to ItemState.Closed. Only one needs to be verified
                Assert.That(mockGitHubEventClient.GetGitHubIssuesToUpdate()[0].IssueUpdate.State, Is.EqualTo(ItemState.Closed), $"rule should have set all processed issues to {ItemState.Closed} and did not.");
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
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
            var scheduledEventProcessing = CreateScheduledEventProcessingInstance();
            await scheduledEventProcessing.CloseStaleIssues(mockGitHubEventClient, scheduledEventPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingScheduledUpdates();
            // Verify the RuleCheck 
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                Assert.That(totalUpdates, Is.EqualTo(expectedUpdates), $"The number of updates should have been {expectedUpdates} but was instead, {totalUpdates}");
                int numIssueUpdates = mockGitHubEventClient.GetGitHubIssuesToUpdate().Count;
                Assert.That(numIssueUpdates, Is.EqualTo(expectedUpdates), $"The number of issue updates should have been {expectedUpdates} but was instead, {numIssueUpdates}");

                // The rule sets all of the Issue's States to ItemState.Closed. Only one needs to be verified
                Assert.That(mockGitHubEventClient.GetGitHubIssuesToUpdate()[0].IssueUpdate.State, Is.EqualTo(ItemState.Closed), $"rule should have set all processed issues to {ItemState.Closed} and did not.");
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
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
            var scheduledEventProcessing = CreateScheduledEventProcessingInstance();
            await scheduledEventProcessing.CloseStalePullRequests(mockGitHubEventClient, scheduledEventPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingScheduledUpdates();
            // Verify the RuleCheck 
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                Assert.That(totalUpdates, Is.EqualTo(expectedUpdates), $"The number of updates should have been {expectedUpdates} but was instead, {totalUpdates}");
                // There should be expectedUpdates/2 issueUpdates and expectedUpdates/2 comments
                int numIssueUpdates = mockGitHubEventClient.GetGitHubIssuesToUpdate().Count;
                Assert.That(numIssueUpdates, Is.EqualTo(expectedUpdates / 2), $"The number of issue updates should have been {expectedUpdates / 2} but was instead, {numIssueUpdates}");
                int numComments = mockGitHubEventClient.GetComments().Count;
                Assert.That(numComments, Is.EqualTo(expectedUpdates / 2), $"The number of comments should have been {expectedUpdates / 2} but was instead, {numComments}");

                // The rule sets all of the Issue's States to ItemState.Closed. Only one needs to be verified
                Assert.That(mockGitHubEventClient.GetGitHubIssuesToUpdate()[0].IssueUpdate.State, Is.EqualTo(ItemState.Closed), $"rule should have set all processed pull requests to {ItemState.Closed} and did not.");
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
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
            var scheduledEventProcessing = CreateScheduledEventProcessingInstance();
            await scheduledEventProcessing.IdentifyStalePullRequests(mockGitHubEventClient, scheduledEventPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingScheduledUpdates();
            // Verify the RuleCheck 
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                Assert.That(totalUpdates, Is.EqualTo(expectedUpdates), $"The number of updates should have been {expectedUpdates} but was instead, {totalUpdates}");
                // There should be expectedUpdates/2 issueUpdates and expectedUpdates/2 comments
                int numIssueUpdates = mockGitHubEventClient.GetGitHubIssuesToUpdate().Count;
                Assert.That(numIssueUpdates, Is.EqualTo(expectedUpdates / 2), $"The number of issue updates should have been {expectedUpdates / 2} but was instead, {numIssueUpdates}");
                int numComments = mockGitHubEventClient.GetComments().Count;
                Assert.That(numComments, Is.EqualTo(expectedUpdates / 2), $"The number of comments should have been {expectedUpdates / 2} but was instead, {numComments}");

                // Verify LabelConstants.NoRecentActivity was added
                Assert.That(mockGitHubEventClient.GetGitHubIssuesToUpdate()[0].IssueUpdate.Labels, Does.Contain(TriageLabelConstants.NoRecentActivity), $"rule should have added {TriageLabelConstants.NoRecentActivity} label and did not.");
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        [Category("static")]
        [TestCaseSource(nameof(StalePullRequestTitleCases))]
        public async Task TestStalePullRequestTitleExclusion(string rule, string title, bool matchesPrefix, bool draft)
        {
            bool excluded = matchesPrefix && !draft;
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = RuleState.On;
            var rawJson = TestHelpers.GetTestEventPayload("Tests.JsonEventPayloads/ScheduledEvent_payload.json");
            var scheduledEventPayload = SimpleJsonSerializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
            bool closeStalePullRequests = rule == RulesConstants.CloseStalePullRequests;

            Issue CreateIssue(int number, string issueTitle)
            {
                return SimpleJsonSerializer.Deserialize<Issue>(JsonSerializer.Serialize(new
                {
                    number,
                    title = issueTitle,
                    state = "open",
                    draft,
                    user = new { login = "FakeUser1" },
                    labels = new[] { new { name = closeStalePullRequests ? TriageLabelConstants.NoRecentActivity : "test-label" } }
                }));
            }

            var issue = CreateIssue(1, title);
            var ordinaryIssue = CreateIssue(2, "Update SDK");
            mockGitHubEventClient.PullRequests[issue.Number] =
                SimpleJsonSerializer.Deserialize<PullRequest>(JsonSerializer.Serialize(new { draft }));
            mockGitHubEventClient.SearchIssuesResultReturn = new SearchIssuesResult(2, false, new[] { issue, ordinaryIssue });

            var scheduledEventProcessing = CreateScheduledEventProcessingInstance();
            await scheduledEventProcessing.ProcessScheduledEvent(mockGitHubEventClient, scheduledEventPayload, rule);

            int expectedPullRequests = excluded ? 1 : 2;
            var updates = mockGitHubEventClient.GetGitHubIssuesToUpdate();
            var comments = mockGitHubEventClient.GetComments();
            Assert.That(await mockGitHubEventClient.ProcessPendingScheduledUpdates(), Is.EqualTo(expectedPullRequests * 2));
            Assert.That(updates.Count, Is.EqualTo(expectedPullRequests));
            Assert.That(comments.Count, Is.EqualTo(expectedPullRequests));
            Assert.That(updates.Exists(update => update.IssueOrPRNumber == issue.Number), Is.EqualTo(!excluded));
            Assert.That(comments.Exists(comment => comment.IssueOrPullRequestNumber == issue.Number), Is.EqualTo(!excluded));
            Assert.That(updates.Exists(update => update.IssueOrPRNumber == ordinaryIssue.Number), Is.True);
            Assert.That(comments.Exists(comment => comment.IssueOrPullRequestNumber == ordinaryIssue.Number), Is.True);
            Assert.That(mockGitHubEventClient.RequestedPullRequests,
                Is.EqualTo(matchesPrefix ? new[] { issue.Number } : new int[0]));
            foreach (var update in updates)
            {
                if (closeStalePullRequests)
                {
                    Assert.That(update.IssueUpdate.State, Is.EqualTo(ItemState.Closed));
                    Assert.That(update.IssueUpdate.StateReason, Is.EqualTo(ItemStateReason.NotPlanned));
                }
                else
                {
                    Assert.That(update.IssueUpdate.Labels, Does.Contain(TriageLabelConstants.NoRecentActivity));
                    Assert.That(update.IssueUpdate.Labels, Does.Contain("test-label"));
                }
            }
        }

        private static IEnumerable<TestCaseData> StalePullRequestTitleCases()
        {
            foreach (string rule in new[] { RulesConstants.IdentifyStalePullRequests, RulesConstants.CloseStalePullRequests })
            {
                foreach (bool draft in new[] { false, true })
                {
                    yield return new TestCaseData(rule, "[AutoPR sdk] Update SDK", true, draft);
                    yield return new TestCaseData(rule, "[AutoPR ", true, draft);
                    yield return new TestCaseData(rule, "[AutoPR  Update SDK", true, draft);
                    yield return new TestCaseData(rule, "[autopr sdk] Update SDK", false, draft);
                    yield return new TestCaseData(rule, "[AutoPr sdk] Update SDK", false, draft);
                    yield return new TestCaseData(rule, "[AutoPR] Update SDK", false, draft);
                    yield return new TestCaseData(rule, "[AutoPR\tUpdate SDK", false, draft);
                    yield return new TestCaseData(rule, " [AutoPR sdk] Update SDK", false, draft);
                    yield return new TestCaseData(rule, "Update [AutoPR sdk]", false, draft);
                    yield return new TestCaseData(rule, "Update SDK", false, draft);
                    yield return new TestCaseData(rule, "", false, draft);
                }
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
            var scheduledEventProcessing = CreateScheduledEventProcessingInstance();
            await scheduledEventProcessing.IdentifyStaleIssues(mockGitHubEventClient, scheduledEventPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingScheduledUpdates();
            // Verify the RuleCheck 
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                Assert.That(totalUpdates, Is.EqualTo(expectedUpdates), $"The number of updates should have been {expectedUpdates} but was instead, {totalUpdates}");
                // There should be expectedUpdates/2 issueUpdates and expectedUpdates/2 comments
                int numIssueUpdates = mockGitHubEventClient.GetGitHubIssuesToUpdate().Count;
                Assert.That(numIssueUpdates, Is.EqualTo(expectedUpdates / 2), $"The number of issue updates should have been {expectedUpdates / 2} but was instead, {numIssueUpdates}");
                int numComments = mockGitHubEventClient.GetComments().Count;
                Assert.That(numComments, Is.EqualTo(expectedUpdates / 2), $"The number of comments should have been {expectedUpdates / 2} but was instead, {numComments}");

                // Verify LabelConstants.NoRecentActivity was added
                Assert.That(mockGitHubEventClient.GetGitHubIssuesToUpdate()[0].IssueUpdate.Labels, Does.Contain(TriageLabelConstants.NoRecentActivity), $"rule should have added {TriageLabelConstants.NoRecentActivity} label and did not.");
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
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
            var scheduledEventProcessing = CreateScheduledEventProcessingInstance();
            await scheduledEventProcessing.LockClosedIssues(mockGitHubEventClient, scheduledEventPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingScheduledUpdates();
            // Verify the RuleCheck 
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                Assert.That(totalUpdates, Is.EqualTo(expectedUpdates), $"The number of updates should have been {expectedUpdates} but was instead, {totalUpdates}");
                int numIssuesToLock = mockGitHubEventClient.GetGitHubIssuesToLock().Count;
                Assert.That(numIssuesToLock, Is.EqualTo(expectedUpdates), $"The number of issues to lock should have been {expectedUpdates} but was instead, {numIssuesToLock}");
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test the EnforceMaxLifeOfIssues scheduled event.
        /// Each item returned from the query will have three updates:
        ///     Issue will be closed
        ///     Issue will have a comment added
        ///     Issue will be locked
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.EnforceMaxLifeOfIssues, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.On)]
        [TestCase(RulesConstants.EnforceMaxLifeOfIssues, "Tests.JsonEventPayloads/ScheduledEvent_payload.json", RuleState.Off)]
        public async Task TestEnforceMaxLifeOfIssues(string rule, string payloadFile, RuleState ruleState)
        {
            // Need something divisible by 3 Because EnforceMaxLifeOfIssues does 3 updates per issue
            // creating 100 results should only result in 34 issues being closed and 34 comments created
            // and 34 issues being locked = 102 expected updates.
            int expectedUpdates = 102;
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            ScheduledEventGitHubPayload scheduledEventPayload = SimpleJsonSerializer.Deserialize<ScheduledEventGitHubPayload>(rawJson);
            // Create the fake issues to update. 
            mockGitHubEventClient.CreateSearchIssuesResult(expectedUpdates, scheduledEventPayload.Repository, ItemState.Open);
            var scheduledEventProcessing = CreateScheduledEventProcessingInstance();
            await scheduledEventProcessing.EnforceMaxLifeOfIssues(mockGitHubEventClient, scheduledEventPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingScheduledUpdates();
            // Verify the RuleCheck 
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                // Create the fake issues to update. 
                Assert.That(totalUpdates, Is.EqualTo(expectedUpdates), $"The number of updates should have been {expectedUpdates} but was instead, {totalUpdates}");
                // There should be expectedUpdates/3 issueUpdates, expectedUpdates/3 comments and expectedUpdates/3 issues to lock
                int numIssueUpdates = mockGitHubEventClient.GetGitHubIssuesToUpdate().Count;
                Assert.That(numIssueUpdates, Is.EqualTo(expectedUpdates / 3), $"The number of issue updates should have been {expectedUpdates / 3} but was instead, {numIssueUpdates}");
                int numComments = mockGitHubEventClient.GetComments().Count;
                Assert.That(numComments, Is.EqualTo(expectedUpdates / 3), $"The number of comments should have been {expectedUpdates / 3} but was instead, {numComments}");
                int numIssuesToLock = mockGitHubEventClient.GetGitHubIssuesToLock().Count;
                Assert.That(numIssuesToLock, Is.EqualTo(expectedUpdates / 3), $"The number of issues to lock should have been {expectedUpdates / 3} but was instead, {numIssuesToLock}");
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        private static ScheduledEventProcessing CreateScheduledEventProcessingInstance()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<ScheduledEventProcessing>();
            return new ScheduledEventProcessing(logger);
        }
    }
}
