using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Octokit;
using Octokit.Internal;
using NUnit.Framework;
using System.IO;
using System;
using System.Data;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class IssueCommentProcessingTests : ProcessingTestBase
    {
        /// <summary>
        /// Test AuthorFeedback rule enabled/disabled, with a payload that would cause updates when enabled.
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <returns></returns>
        [Category("static")]
        [TestCase(RulesConstants.AuthorFeedback, "Tests.JsonEventPayloads/AuthorFeedBack_issue_comment_created.json", RuleState.On)]
        [TestCase(RulesConstants.AuthorFeedback, "Tests.JsonEventPayloads/AuthorFeedBack_issue_comment_created.json", RuleState.Off)]
        public async Task TestAuthorFeedbackEnabledDisabled(string rule, string payloadFile, RuleState ruleState)
        {
            MockGitHubEventClient mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            string rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            IssueCommentPayload issueCommentPayload = SimpleJsonSerializer.Deserialize<IssueCommentPayload>(rawJson);
            IssueCommentProcessing.AuthorFeedback(mockGitHubEventClient, issueCommentPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            int totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number);
            int numComments = mockGitHubEventClient.GetComments().Count;
            if (RuleState.On == ruleState)
            {
                // There should be two updates, an IssueUpdate with the label updates and a single comment
                Assert.AreEqual(2, totalUpdates, $"The number of updates should have been 2 but was instead, {totalUpdates}");
                Assert.AreEqual(1, numComments, $"{rule} should have created a single comment but {numComments} comments were created.");

                // Retrieve the IssueUpdate and verify the expected changes
                IssueUpdate issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTeamAttention} removed and {LabelConstants.NeedsAuthorFeedback} added.");
                // Verify that NeedsTeamAttention was added
                Assert.True(issueUpdate.Labels.Contains(LabelConstants.NeedsTeamAttention), $"IssueUpdate does not contain {LabelConstants.NeedsTeamAttention} label");
                // Verify that NeedsAuthorFeedback was removed
                Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsAuthorFeedback), $"IssueUpdate contains {LabelConstants.NeedsAuthorFeedback} label which should have been removed");
            }
            else
            {
                Assert.AreEqual(0, numComments, $"{rule} is {ruleState} and should not have created any comments but {numComments} comments were created.");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
            return;
        }

        /// <summary>
        /// Test ResetIssueActivity with issue_comment payload.
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <returns></returns>
        [TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_comment_created.json", RuleState.On)]
        [TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_comment_created.json", RuleState.Off)]
        public async Task TestResetIssueActivityEnabledDisabled(string rule, string payloadFile, RuleState ruleState)
        {
            MockGitHubEventClient mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            string rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            IssueCommentPayload issueCommentPayload = SimpleJsonSerializer.Deserialize<IssueCommentPayload>(rawJson);
            IssueCommentProcessing.ResetIssueActivity(mockGitHubEventClient, issueCommentPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            int totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should be one update, an IssueUpdate with the label NoRecentActivity removed
                Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                // Retrieve the IssueUpdate and verify the expected changes
                IssueUpdate issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                Assert.IsNotNull(issueUpdate, $"IssueUpdate is null. {rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NoRecentActivity} removed.");
                // Verify that NoRecentActivity was removed
                Assert.False(issueUpdate.Labels.Contains(LabelConstants.NoRecentActivity), $"IssueUpdate contains {LabelConstants.NoRecentActivity} label which should have been removed");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"The number of updates should have been 0 but was instead, {totalUpdates}");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
            return;
        }
        /// <summary>
        /// Test ReopenIssue with issue_comment payload.
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <returns></returns>
        [TestCase(RulesConstants.ReopenIssue, "Tests.JsonEventPayloads/ReopenIssue_issue_comment_created.json", RuleState.On)]
        [TestCase(RulesConstants.ReopenIssue, "Tests.JsonEventPayloads/ReopenIssue_issue_comment_created.json", RuleState.Off)]
        public async Task TestReopenIssueEnabledDisabled(string rule, string payloadFile, RuleState ruleState)
        {
            MockGitHubEventClient mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            string rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            IssueCommentPayload issueCommentPayload = SimpleJsonSerializer.Deserialize<IssueCommentPayload>(rawJson);
            IssueCommentProcessing.ReopenIssue(mockGitHubEventClient, issueCommentPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            int totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should be one update, an IssueUpdate with the labels NeedsAuthorFeedback and NoRecentActivity removed
                // and NeedsTeamAttention added
                Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                // Retrieve the IssueUpdate and verify the expected changes
                IssueUpdate issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                Assert.IsNotNull(issueUpdate, $"IssueUpdate is null. {rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsAuthorFeedback} and {LabelConstants.NoRecentActivity} removed and {LabelConstants.NeedsTeamAttention} added.");

                // Verify that the NeedsTeamAttention label was added
                Assert.True(issueUpdate.Labels.Contains(LabelConstants.NeedsTeamAttention), $"IssueUpdate does not contain {LabelConstants.NeedsTeamAttention} label");

                // Verify that NeedsAuthorFeedback and NoRecentActivity labels were removed
                Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsAuthorFeedback), $"IssueUpdate contains {LabelConstants.NeedsAuthorFeedback} label which should have been removed");
                Assert.False(issueUpdate.Labels.Contains(LabelConstants.NoRecentActivity), $"IssueUpdate contains {LabelConstants.NoRecentActivity} label which should have been removed");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"The number of updates should have been 0 but was instead, {totalUpdates}");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
            return;
        }
    }
}
