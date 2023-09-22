using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using NUnit.Framework;
using Octokit;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Tests.Static
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class PullRequestProcessingTests : ProcessingTestBase
    {

        /// <summary>
        /// Test ResetPullRequestActivity rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: pull request opened
        /// Conditions: Pull request has no labels
        /// Resulting Action: 
        ///     Evaluate the path for each file in the PR, if the path has a label, add the label to the issue
        ///     If the sender is not a Collaborator OR, if they are a collaborator without Write/Admin permissions
        ///         Add "customer-reported" label
        ///         Add "Community Contribution" label
        ///         Create issue comment: "Thank you for your contribution @{issueAuthor} ! We will review the pull request and get back to you soon."
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="hasWriteOrAdmin">Whether or not the PR creator has write or admin permissions</param>
        [Category("static")]
        [NonParallelizable]
        [TestCase(RulesConstants.PullRequestTriage, "Tests.JsonEventPayloads/PullRequestTriage_pr_opened_no_labels.json", RuleState.On, true, true)]
        [TestCase(RulesConstants.PullRequestTriage, "Tests.JsonEventPayloads/PullRequestTriage_pr_opened_no_labels.json", RuleState.Off, false, false)]
        [TestCase(RulesConstants.PullRequestTriage, "Tests.JsonEventPayloads/PullRequestTriage_pr_opened_no_labels.json", RuleState.On, true, false)]
        [TestCase(RulesConstants.PullRequestTriage, "Tests.JsonEventPayloads/PullRequestTriage_pr_opened_no_labels.json", RuleState.On, false, true)]
        [TestCase(RulesConstants.PullRequestTriage, "Tests.JsonEventPayloads/PullRequestTriage_pr_opened_no_labels.json", RuleState.On, false, false)]
        public async Task TestPullRequestTriage(string rule, string payloadFile, RuleState ruleState, bool isMemberOfOrg, bool hasWriteOrAdmin)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            mockGitHubEventClient.UserHasPermissionsReturn = hasWriteOrAdmin;
            mockGitHubEventClient.IsUserMemberOfOrgReturn = isMemberOfOrg;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var prEventPayload = PullRequestProcessing.DeserializePullRequest(rawJson, SimpleJsonSerializer);

            // Set the path to the fake CODEOWNERS file to be used for testing
            CodeOwnerUtils.ResetCodeOwnerEntries();
            CodeOwnerUtils.codeOwnersFilePathOverride = "Tests.FakeCodeowners/PullRequestTriage_CODEOWNERS";
            Dictionary<string, string> prFilesAndLabels = new Dictionary<string, string>
            {
                { "/files/filePath2/file2", "FakeLabel2" },
                { "/files/filePath4/file1", "FakeLabel4" },
                { "/files/filePath7/file3", "FakeLabel7" },
                { "/files/filePath8/file4", "FakeLabel8" }
            };
            mockGitHubEventClient.CreateFakePullRequestFiles(prFilesAndLabels.Keys.ToList());
            await PullRequestProcessing.PullRequestTriage(mockGitHubEventClient, prEventPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(prEventPayload.Repository.Id, prEventPayload.PullRequest.Number);
            if (RuleState.On == ruleState)
            {
                // Regardless of whether or not the user has Write or Admin permissions, the prFiles should cause 4 labels to get added
                // which means an issueUpdate will be created
                int expectedUpdates = 1;

                if (hasWriteOrAdmin || isMemberOfOrg)
                {
                    // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                    Assert.AreEqual(expectedUpdates, totalUpdates, $"The number of updates for a user having Write or Admin permission or being a member of Azure org should have been {expectedUpdates} but was instead, {totalUpdates}");
                }
                // If the user doesn't have Write or Admin permissions then "customer-reported" and "Community Contribution" labels
                // will be added and a single comment will be created
                else
                {
                    expectedUpdates++;
                    // Along with the label updates, there should also be a comment added.
                    Assert.AreEqual(expectedUpdates, totalUpdates, $"The number of updates for a user without Write or Admin permission or being a member of Azure org should have been {expectedUpdates} but was instead, {totalUpdates}");
                }

                // Regardless of permissions, all of the labels based on PR file paths should be added
                foreach (string label in prFilesAndLabels.Values.ToList())
                {
                    Assert.True(mockGitHubEventClient.GetLabelsToAdd().Contains(label), $"Labels to add should contain {label} which should have been added because of the file paths in the PR but was not.");
                }

                // If the user is not part of the Azure org AND they don't have write or admin collaborator permissions
                // then customer-reported and community-contribution labels should have been added along with a comment
                if (!isMemberOfOrg && !hasWriteOrAdmin)
                {
                    Assert.True(mockGitHubEventClient.GetLabelsToAdd().Contains(LabelConstants.CustomerReported), $"User does not have write or admin permission, IssueUpdate should contain {LabelConstants.CustomerReported}.");
                    Assert.True(mockGitHubEventClient.GetLabelsToAdd().Contains(LabelConstants.CommunityContribution), $"User does not have write or admin permission, IssueUpdate should contain {LabelConstants.CommunityContribution}.");
                    Assert.AreEqual(1, mockGitHubEventClient.GetComments().Count, "Without admin or write permission there should have been a comment added.");
                }
                else
                {
                    Assert.False(mockGitHubEventClient.GetLabelsToAdd().Contains(LabelConstants.CustomerReported), $"User has write or admin permission or is a member of Azure, IssueUpdate should not contain {LabelConstants.CustomerReported}.");
                    Assert.False(mockGitHubEventClient.GetLabelsToAdd().Contains(LabelConstants.CommunityContribution), $"User has write or admin permission or is a member of Azure, IssueUpdate should not contain {LabelConstants.CommunityContribution}.");
                    Assert.AreEqual(0, mockGitHubEventClient.GetComments().Count, "User has write or admin permission or is a member of Azure, there should not have been a comment added.");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test ResetPullRequestActivity rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Conditions for pull_request triggers, except for merge
        ///     Pull request is open.
        ///     Action is reopen, synchronize (changed pushed) or review requested
        /// Conditions for pull_request merge
        ///     Pull request is closed.
        ///     Action is open with PullRequest.Merged = true
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.ResetPullRequestActivity, "Tests.JsonEventPayloads/ResetPullRequestActivity_pr_reopened.json", RuleState.On)]
        [TestCase(RulesConstants.ResetPullRequestActivity, "Tests.JsonEventPayloads/ResetPullRequestActivity_pr_reopened.json", RuleState.Off)]
        [TestCase(RulesConstants.ResetPullRequestActivity, "Tests.JsonEventPayloads/ResetPullRequestActivity_pr_review_requested.json", RuleState.On)]
        [TestCase(RulesConstants.ResetPullRequestActivity, "Tests.JsonEventPayloads/ResetPullRequestActivity_pr_synchronize.json", RuleState.On)]
        [TestCase(RulesConstants.ResetPullRequestActivity, "Tests.JsonEventPayloads/ResetPullRequestActivity_pr_closed_merged.json", RuleState.On)]
        public async Task TestResetPullRequestActivity(string rule, string payloadFile, RuleState ruleState)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var prEventPayload = PullRequestProcessing.DeserializePullRequest(rawJson, SimpleJsonSerializer);
            PullRequestProcessing.ResetPullRequestActivity(mockGitHubEventClient, prEventPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(prEventPayload.Repository.Id, prEventPayload.PullRequest.Number);
            if (RuleState.On == ruleState)
            {
                // There should be one update, the NoRecentActivity label removed
                Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                // Verify that NeedsAuthorFeedback was removed
                Assert.True(mockGitHubEventClient.GetLabelsToRemove().Contains(LabelConstants.NoRecentActivity), $"Labels to remove should contain {LabelConstants.NoRecentActivity} and does not.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test ResetPullRequestActivity rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Conditions for pull_request triggers, except for merge
        ///     Pull request is open.
        ///     Action is reopen, synchronize (changed pushed) or review requested
        /// Conditions for pull_request merge
        ///     Pull request is closed.
        ///     Action is open with PullRequest.Merged = true
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="approvedReviews">Number of approved reviews the PR has</param>
        /// <param name="notApprovedReviews">Number of not approved reviews the PR has</param>
        [Category("static")]
        [TestCase(RulesConstants.ResetApprovalsForUntrustedChanges, "Tests.JsonEventPayloads/ResetApprovalsForUntrustedChanges_pr_synchronize.json", RuleState.Off, 0, 0)]
        [TestCase(RulesConstants.ResetApprovalsForUntrustedChanges, "Tests.JsonEventPayloads/ResetApprovalsForUntrustedChanges_pr_synchronize.json", RuleState.On, 0, 0)]
        [TestCase(RulesConstants.ResetApprovalsForUntrustedChanges, "Tests.JsonEventPayloads/ResetApprovalsForUntrustedChanges_pr_synchronize.json", RuleState.On, 2, 0)]
        [TestCase(RulesConstants.ResetApprovalsForUntrustedChanges, "Tests.JsonEventPayloads/ResetApprovalsForUntrustedChanges_pr_synchronize.json", RuleState.On, 0, 3)]
        [TestCase(RulesConstants.ResetApprovalsForUntrustedChanges, "Tests.JsonEventPayloads/ResetApprovalsForUntrustedChanges_pr_synchronize.json", RuleState.On, 4, 5)]
        public async Task TestResetApprovalsForUntrustedChanges(string rule, string payloadFile, RuleState ruleState, int approvedReviews, int notApprovedReviews)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var prEventPayload = PullRequestProcessing.DeserializePullRequest(rawJson, SimpleJsonSerializer);

            // Set the return value for the permission check. The rull will only process if the user does not have permissions.
            mockGitHubEventClient.UserHasPermissionsReturn = false;
            // Create the fake reviews that the rule will requery for
            mockGitHubEventClient.CreateFakeReviewsForPullRequest(approvedReviews, notApprovedReviews);
            await PullRequestProcessing.ResetApprovalsForUntrustedChanges(mockGitHubEventClient, prEventPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(prEventPayload.Repository.Id, prEventPayload.PullRequest.Number);
            if (RuleState.On == ruleState)
            {
                // Regardless of whether or not there are reviews to process, if someone untrusted pushes changes, there will
                // always be one comment. The total number of updates should be 1 (comment) + approvedReviews
                int expectedUpdates = 1 + approvedReviews;
                Assert.AreEqual(expectedUpdates, totalUpdates, $"The number of updates should have been {expectedUpdates} (1 + number of approved reviews) but was instead, {totalUpdates}");

                int numComments = mockGitHubEventClient.GetComments().Count;
                Assert.AreEqual(1, numComments, $"There should have been a single comment created but instead {numComments} were created.");
                int numDismissedReviews = mockGitHubEventClient.GetReviewDismissals().Count;
                Assert.AreEqual(approvedReviews, numDismissedReviews, $"The number of approved reviews {approvedReviews} does not equal the number of dismissed reviews {numDismissedReviews}.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }
    }
}
