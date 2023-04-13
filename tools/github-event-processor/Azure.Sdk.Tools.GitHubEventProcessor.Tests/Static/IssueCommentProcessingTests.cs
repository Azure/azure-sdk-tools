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
using NUnit.Framework.Interfaces;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Tests.Static
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class IssueCommentProcessingTests : ProcessingTestBase
    {
        /// <summary>
        /// Test AuthorFeedback rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.AuthorFeedback, "Tests.JsonEventPayloads/AuthorFeedBack_issue_comment_created.json", RuleState.On)]
        [TestCase(RulesConstants.AuthorFeedback, "Tests.JsonEventPayloads/AuthorFeedBack_issue_comment_created.json", RuleState.Off)]
        public async Task TestAuthorFeedback(string rule, string payloadFile, RuleState ruleState)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueCommentPayload = SimpleJsonSerializer.Deserialize<IssueCommentPayload>(rawJson);
            IssueCommentProcessing.AuthorFeedback(mockGitHubEventClient, issueCommentPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should be 2 updates, one label being added and one
                Assert.AreEqual(2, totalUpdates, $"The number of updates should have been 3 but was instead, {totalUpdates}");
                // Verify that NeedsTeamAttention was added
                Assert.True(mockGitHubEventClient.GetLabelsToAdd().Contains(LabelConstants.NeedsTeamAttention), $"Labels to Add list does not contain {LabelConstants.NeedsTeamAttention}.");
                // Verify that NeedsAuthorFeedback was removed
                Assert.True(mockGitHubEventClient.GetLabelsToRemove().Contains(LabelConstants.NeedsAuthorFeedback), $"Lables to Remove list does not contain {LabelConstants.NeedsAuthorFeedback}.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have been any updates but {totalUpdates} comments were created.");
            }
        }

        /// <summary>
        /// Test ResetIssueActivity enabled/disabled with an issue_comment payload that would cause updates if enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_comment_created.json", RuleState.On)]
        [TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_comment_created.json", RuleState.Off)]
        public async Task TestResetIssueActivity(string rule, string payloadFile, RuleState ruleState)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueCommentPayload = SimpleJsonSerializer.Deserialize<IssueCommentPayload>(rawJson);
            IssueCommentProcessing.ResetIssueActivity(mockGitHubEventClient, issueCommentPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should be one update, the label NoRecentActivity removed
                Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                // Verify that NoRecentActivity is in the remove list
                Assert.True(mockGitHubEventClient.GetLabelsToRemove().Contains(LabelConstants.NoRecentActivity), $"Labels to remove list does not contain {LabelConstants.NoRecentActivity}.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"The number of updates should have been 0 but was instead, {totalUpdates}");
            }
        }

        /// <summary>
        /// Test ReopenIssue enabled/disabled with issue_comment payload that would cause updates if enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.ReopenIssue, "Tests.JsonEventPayloads/ReopenIssue_issue_comment_created.json", RuleState.On)]
        [TestCase(RulesConstants.ReopenIssue, "Tests.JsonEventPayloads/ReopenIssue_issue_comment_created.json", RuleState.Off)]
        public async Task TestReopenIssue(string rule, string payloadFile, RuleState ruleState)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var updateJson = TestHelpers.SetClosedByDateToYesterday(rawJson);
            var issueCommentPayload = SimpleJsonSerializer.Deserialize<IssueCommentPayload>(updateJson);
            IssueCommentProcessing.ReopenIssue(mockGitHubEventClient, issueCommentPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should be 4 updates, the issue state change, 1 label added and 2 labels removed
                Assert.AreEqual(4, totalUpdates, $"The number of updates should have been 4 but was instead, {totalUpdates}");

                // Retrieve the IssueUpdate and verify the expected changes
                var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                Assert.IsNotNull(issueUpdate, $"IssueUpdate is null. {rule} is {ruleState} and should have produced an IssueUpdate with its State being {ItemState.Open}.");
                Assert.AreEqual(issueUpdate.State, ItemState.Open, $"Issue's State should be {ItemState.Open} but was {issueUpdate.State}");

                // Verify that the NeedsTeamAttention label was added
                Assert.True(mockGitHubEventClient.GetLabelsToAdd().Contains(LabelConstants.NeedsTeamAttention), $"Labels to add should contain {LabelConstants.NeedsTeamAttention} and does not.");

                // Verify that NeedsAuthorFeedback and NoRecentActivity labels were removed
                Assert.True(mockGitHubEventClient.GetLabelsToRemove().Contains(LabelConstants.NeedsAuthorFeedback), $"Lables to remove should contain {LabelConstants.NeedsAuthorFeedback} and does not.");
                Assert.True(mockGitHubEventClient.GetLabelsToRemove().Contains(LabelConstants.NoRecentActivity), $"Lables to remove should contain {LabelConstants.NoRecentActivity} and does not.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates but produced {totalUpdates} updates.");
            }
        }

        /// <summary>
        /// Test DeclineToReopenIssue enabled/disabled with issue_comment payload that would cause updates if enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        [Category("static")]
        [TestCase(RulesConstants.DeclineToReopenIssue, "Tests.JsonEventPayloads/DeclineToReopenIssue_issue_comment_created.json", RuleState.On)]
        [TestCase(RulesConstants.DeclineToReopenIssue, "Tests.JsonEventPayloads/DeclineToReopenIssue_issue_comment_created.json", RuleState.Off)]
        public async Task TestDeclineToReopenIssue(string rule, string payloadFile, RuleState ruleState)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueCommentPayload = SimpleJsonSerializer.Deserialize<IssueCommentPayload>(rawJson);
            // Set the return value for the DoesUserHavePermission check. The rule requires the user who created the
            // comment to have PermissionLevel.None and that call needs to return true.
            mockGitHubEventClient.UserHasPermissionsReturn = true;
            await IssueCommentProcessing.DeclineToReopenIssue(mockGitHubEventClient, issueCommentPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number);
            var numComments = mockGitHubEventClient.GetComments().Count;
            if (RuleState.On == ruleState)
            {
                // There should be one update, a comment.
                Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                // Verify the IssueUpdate is null, the rule does not change that
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
                // Verify that a single comment was created
                Assert.AreEqual(1, numComments, $"{rule} should have created a single comment but {numComments} comments were created.");
            }
            else
            {
                Assert.AreEqual(0, numComments, $"{rule} is {ruleState} and should not have created any comments but {numComments} comments were created.");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
        }

        /// <summary>
        /// Test DeclineToReopenIssue enabled/disabled with issue_comment payload that would cause updates if enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Same user means the user who created the Issue also created the IssueComment. If the user who created the
        /// issue also created the comment, then whether or not the user has admin or write permissions is irrelevant.
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="userHasAdminOrWritePermission">Whether or not the user has Admin or Write permission.</param>
        [Category("static")]
        [TestCase(RulesConstants.IssueAddressedCommands, "Tests.JsonEventPayloads/IssueAddressedCommands_issue_comment_created_same_user.json", RuleState.On, true)]
        [TestCase(RulesConstants.IssueAddressedCommands, "Tests.JsonEventPayloads/IssueAddressedCommands_issue_comment_created_same_user.json", RuleState.On, false)]
        [TestCase(RulesConstants.IssueAddressedCommands, "Tests.JsonEventPayloads/IssueAddressedCommands_issue_comment_created_same_user.json", RuleState.Off, false)]
        public async Task TestIssueAddressedCommands_SameUser(string rule, string payloadFile, RuleState ruleState, bool userHasAdminOrWritePermission)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueCommentPayload = SimpleJsonSerializer.Deserialize<IssueCommentPayload>(rawJson);
            // Set the return value for the DoesUserHavePermission check. Since the comment creator is the same user
            // that created the issue, whether or not they have admin or write permissions should have no effect.
            mockGitHubEventClient.UserHasPermissionsReturn = userHasAdminOrWritePermission;
            await IssueCommentProcessing.IssueAddressedCommands(mockGitHubEventClient, issueCommentPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should 3 updates, the IssueUpdate with State set to ItemState.Open, 1 label removed and 1 added
                Assert.AreEqual(3, totalUpdates, $"The number of updates should have been 3 but was instead, {totalUpdates}");

                var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                // Verify the IssueUpdate's State = ItemState.Open
                Assert.AreEqual(issueUpdate.State, ItemState.Open, $"IssueUpdate's state should be ItemState.Open and was not.");
                // IssueAddressed label has been removed
                Assert.True(mockGitHubEventClient.GetLabelsToRemove().Contains(LabelConstants.IssueAddressed), $"Labels to remove should contain {LabelConstants.IssueAddressed} and does not.");
                // NeedsTeamAttention has been added
                Assert.True(mockGitHubEventClient.GetLabelsToAdd().Contains(LabelConstants.NeedsTeamAttention), $"Labels to add should contain {LabelConstants.NeedsTeamAttention} and does not.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"The number of updates should have been 0 but was instead, {totalUpdates}");
            }
        }

        /// <summary>
        /// Test DeclineToReopenIssue enabled/disabled with issue_comment payload that would cause updates if enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Different user means the user who created the Issue is not the same user that created the IssueComment and
        /// different actions are performed if the user has admin or write permission than if they don't.
        /// We test the rule being on/off in the Same User test and that will not be tested here.
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="userHasAdminOrWritePermission">Whether or not the user has Admin or Write permission.</param>
        [Category("static")]
        [TestCase(RulesConstants.IssueAddressedCommands, "Tests.JsonEventPayloads/IssueAddressedCommands_issue_comment_created_different_user.json", true)]
        [TestCase(RulesConstants.IssueAddressedCommands, "Tests.JsonEventPayloads/IssueAddressedCommands_issue_comment_created_different_user.json", false)]
        public async Task TestIssueAddressedCommands_DifferentUser(string rule, string payloadFile, bool userHasAdminOrWritePermission)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueCommentPayload = SimpleJsonSerializer.Deserialize<IssueCommentPayload>(rawJson);
            // Set the return value for the permission check. Since the comment creator is the 
            mockGitHubEventClient.UserHasPermissionsReturn = userHasAdminOrWritePermission;
            await IssueCommentProcessing.IssueAddressedCommands(mockGitHubEventClient, issueCommentPayload);

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueCommentPayload.Repository.Id, issueCommentPayload.Issue.Number);
            var numComments = mockGitHubEventClient.GetComments().Count;
            if (userHasAdminOrWritePermission)
            {

                // There should 3 updates, the IssueUpdate with State set to ItemState.Open, 1 label removed and 1 added
                Assert.AreEqual(3, totalUpdates, $"The number of updates should have been 3 but was instead, {totalUpdates}");

                var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                // Verify the IssueUpdate's State = ItemState.Open
                Assert.AreEqual(issueUpdate.State, ItemState.Open, $"IssueUpdate's state should be ItemState.Open and was not.");
                // IssueAddressed label has been removed
                Assert.True(mockGitHubEventClient.GetLabelsToRemove().Contains(LabelConstants.IssueAddressed), $"Labels to remove should contain {LabelConstants.IssueAddressed} and does not.");
                // NeedsTeamAttention has been added
                Assert.True(mockGitHubEventClient.GetLabelsToAdd().Contains(LabelConstants.NeedsTeamAttention), $"Labels to add should contain {LabelConstants.NeedsTeamAttention} and does not.");
            }
            else
            {
                // If the user does not have admin or write permission then there should be a single comment
                Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");
                Assert.AreEqual(1, numComments, $"Without admin or write permissions, 1 comment should have been created but {numComments} comments were created.");
            }
        }
    }
}
