using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using Microsoft.Extensions.Logging;
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
        /// <param name="commenterHasPermissionOrIsAuthor">Whether or not the commenter has write/admin permissions or is the PR author</param>
        [Category("static")]
        [TestCase(RulesConstants.ResetPullRequestActivity, "Tests.JsonEventPayloads/ResetPullRequestActivity_pr_comment_created_creator_is_author.json", RuleState.On, true)]
        [TestCase(RulesConstants.ResetPullRequestActivity, "Tests.JsonEventPayloads/ResetPullRequestActivity_pr_comment_created_creator_is_author.json", RuleState.Off, true)]
        [TestCase(RulesConstants.ResetPullRequestActivity, "Tests.JsonEventPayloads/ResetPullRequestActivity_pr_comment_created_creator_is_not_author.json", RuleState.On, true)]
        [TestCase(RulesConstants.ResetPullRequestActivity, "Tests.JsonEventPayloads/ResetPullRequestActivity_pr_comment_created_creator_is_not_author.json", RuleState.On, false)]
        public async Task TestResetPullRequestActivity(string rule, string payloadFile, RuleState ruleState, bool commenterHasPermissionOrIsAuthor)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var prCommentPayload = SimpleJsonSerializer.Deserialize<IssueCommentPayload>(rawJson);
            // Set the return value for the permission check. In the case where the commenter is not the author, the label
            // will only be removed if they have write or admin collaborator permissions.
            mockGitHubEventClient.UserHasPermissionsReturn = commenterHasPermissionOrIsAuthor;
            var pullRequestCommentProcessing = CreatePullRequestCommentInstance();
            await pullRequestCommentProcessing.ResetPullRequestActivity(mockGitHubEventClient, prCommentPayload);

            // Verify the RuleCheck 
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(prCommentPayload.Repository.Id, prCommentPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                if (commenterHasPermissionOrIsAuthor)
                {
                    // There should be one update, the no-recent-activity label removed
                    Assert.That(totalUpdates, Is.EqualTo(1), $"The number of updates should have been 1 but was instead, {totalUpdates}");
                    // Verify that NeedsAuthorFeedback was removed
                    Assert.That(mockGitHubEventClient.GetLabelsToRemove(), Does.Contain(TriageLabelConstants.NoRecentActivity), $"Labels to remove should contain {TriageLabelConstants.NoRecentActivity} and does not.");
                }
                else
                {
                    Assert.That(totalUpdates, Is.EqualTo(0), $"Without Admin or Write permissions the number of updates should have been 0 but was instead, {totalUpdates}");
                }
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"The number of updates should have been 0 but was instead, {totalUpdates}");
            }
        }

        /// <summary>
        /// Test ReopenPullRequest rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="commenterHasPermissionOrIsAuthor">Whether or not the commenter has write/admin permissions or is the PR author</param>
        [Category("static")]
        [TestCase(RulesConstants.ReopenPullRequest, "Tests.JsonEventPayloads/ReopenPullRequest_pr_comment_created_creator_is_author.json", RuleState.On, true)]
        [TestCase(RulesConstants.ReopenPullRequest, "Tests.JsonEventPayloads/ReopenPullRequest_pr_comment_created_creator_is_author.json", RuleState.Off, true)]
        [TestCase(RulesConstants.ReopenPullRequest, "Tests.JsonEventPayloads/ReopenPullRequest_pr_comment_created_creator_is_not_author.json", RuleState.On, true)]
        [TestCase(RulesConstants.ReopenPullRequest, "Tests.JsonEventPayloads/ReopenPullRequest_pr_comment_created_creator_is_not_author.json", RuleState.On, false)]
        public async Task TestReopenPullRequest(string rule, string payloadFile, RuleState ruleState, bool commenterHasPermissionOrIsAuthor)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var prCommentPayload = SimpleJsonSerializer.Deserialize<IssueCommentPayload>(rawJson);
            // Set the return value for the permission check. In the case where the commenter is not the author, the label
            // will only be removed if they have write or admin collaborator permissions and, if not, a comment will be created.
            mockGitHubEventClient.UserHasPermissionsReturn = commenterHasPermissionOrIsAuthor;
            var pullRequestCommentProcessing = CreatePullRequestCommentInstance();
            await pullRequestCommentProcessing.ReopenPullRequest(mockGitHubEventClient, prCommentPayload);

            // Verify the RuleCheck 
            Assert.That(mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), Is.EqualTo(ruleState == RuleState.On), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(prCommentPayload.Repository.Id, prCommentPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                if (commenterHasPermissionOrIsAuthor)
                {
                    // There should be two updates, an IssueUpdate with the State set to ItemState.Open and the no-recent-activity label removed
                    Assert.That(totalUpdates, Is.EqualTo(2), $"If the commenter has permissions or is the author, the number of updates should have been 2 but was instead, {totalUpdates}");

                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    // Verify the IssueUpdate is not null
                    Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate.");
                    // Verify the IssueUpdate contains the following changes:
                    // State = ItemState.Open
                    Assert.That(issueUpdate.State, Is.EqualTo(ItemState.Open), $"IssueUpdate's state should be {ItemState.Open} and was not.");
                    // Verify that NoRecentActivity was removed
                    Assert.That(mockGitHubEventClient.GetLabelsToRemove(), Does.Contain(TriageLabelConstants.NoRecentActivity), $"Labels to remove should contain {TriageLabelConstants.NoRecentActivity} and does not.");
                }
                else
                {
                    // If the commenter isn't the author and doesn't have permissions then there should be 1 comment created
                    Assert.That(totalUpdates, Is.EqualTo(1), $"If the commenter has permissions or is the author, there should be have been 1 update, a comment, but was instead {totalUpdates}");
                    // There should be a single comment created
                    int numComments = mockGitHubEventClient.GetComments().Count;
                    Assert.That(numComments, Is.EqualTo(1), $"There should have been a single comment created but instead {numComments} were created.");
                }
            }
            else
            {
                Assert.That(totalUpdates, Is.EqualTo(0), $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        private static PullRequestCommentProcessing CreatePullRequestCommentInstance()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<PullRequestCommentProcessing>();
            return new PullRequestCommentProcessing(logger);
        }
    }
}
