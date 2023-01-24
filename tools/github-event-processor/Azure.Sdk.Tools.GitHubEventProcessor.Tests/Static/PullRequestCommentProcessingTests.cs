using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using NUnit.Framework;
using Octokit;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Tests.Static
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class PullRequestCommentProcessingTests : ProcessingTestBase
    {
        /// <summary>
        /// Test ResetPullRequestActivity rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <returns></returns>
        [Category("static")]
        [TestCase(RulesConstants.ResetPullRequestActivity, "Tests.JsonEventPayloads/ResetPullRequestActivity_pr_comment_created.json", RuleState.On)]
        [TestCase(RulesConstants.ResetPullRequestActivity, "Tests.JsonEventPayloads/ResetPullRequestActivity_pr_comment_created.json", RuleState.Off)]
        public async Task TestResetPullRequestActivity(string rule, string payloadFile, RuleState ruleState)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var prCommentPayload = SimpleJsonSerializer.Deserialize<IssueCommentPayload>(rawJson);
            // Set the return value for the permission check. The rull will only process if the user does not have permissions.
            mockGitHubEventClient.UserHasPermissionsReturn = false;
            await PullRequestCommentProcessing.ResetPullRequestActivity(mockGitHubEventClient, prCommentPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(prCommentPayload.Repository.Id, prCommentPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should be one update, an IssueUpdate with the no-recent-activity removed
                Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                // Retrieve the IssueUpdate and verify the expected changes
                var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NoRecentActivity} removed.");
                // Verify that NeedsAuthorFeedback was removed
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
        /// Test ReopenPullRequest rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <returns></returns>
        [Category("static")]
        [TestCase(RulesConstants.ReopenPullRequest, "Tests.JsonEventPayloads/ReopenPullRequest_pr_comment_created.json", RuleState.On)]
        [TestCase(RulesConstants.ReopenPullRequest, "Tests.JsonEventPayloads/ReopenPullRequest_pr_comment_created.json", RuleState.Off)]
        public async Task TestReopenPullRequest(string rule, string payloadFile, RuleState ruleState)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var prCommentPayload = SimpleJsonSerializer.Deserialize<IssueCommentPayload>(rawJson);
            // Set the return value for the permission check. The rull will only process if the user does not have permissions.
            mockGitHubEventClient.UserHasPermissionsReturn = false;
            await PullRequestCommentProcessing.ReopenPullRequest(mockGitHubEventClient, prCommentPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(prCommentPayload.Repository.Id, prCommentPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should be one update, an IssueUpdate with the no-recent-activity removed
                Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                // Verify the IssueUpdate is not null
                Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate.");
                // Verify the IssueUpdate contains the following changes:
                // State = ItemState.Open
                Assert.AreEqual(issueUpdate.State, ItemState.Open, $"IssueUpdate's state should be ItemState.Open and was not.");
                // Verify that NoRecentActivity was removed
                Assert.False(issueUpdate.Labels.Contains(LabelConstants.NoRecentActivity), $"IssueUpdate contains {LabelConstants.NoRecentActivity} label which should have been removed");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
            return;
        }
    }
}
