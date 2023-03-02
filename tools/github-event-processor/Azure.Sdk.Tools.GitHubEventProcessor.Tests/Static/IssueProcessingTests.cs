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

namespace Azure.Sdk.Tools.GitHubEventProcessor.Tests.Static
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class IssueProcessingTests : ProcessingTestBase
    {
        /// <summary>
        /// TBD: InitialIssueTriage rule is not yet complete as it requires a CODEOWNERS overhaul that is not complete.
        /// The payload for this rule to process requires an issues opened event with no assignee and no labels.
        /// Until the CODEOWNERS changes are complete, processing is as follows:
        /// Trigger: issues opened
        /// Conditions: Issue has no assignee
        ///             Issue has no labels
        /// Resulting Action:Query the AI Service
        ///     If the AI Service 
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="AIServiceReturnsLabels">Whether or not the AI Service should return labels</param>
        /// <returns></returns>
        [TestCase(RulesConstants.InitialIssueTriage, "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json", RuleState.On, true)]
        [TestCase(RulesConstants.InitialIssueTriage, "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json", RuleState.On, false)]
        [TestCase(RulesConstants.InitialIssueTriage, "Tests.JsonEventPayloads/InitialIssueTriage_issue_opened_no_labels_no_assignee.json", RuleState.Off, false)]
        public async Task TestInitialIssueTriage(string rule, string payloadFile, RuleState ruleState, bool AIServiceReturnsLabels)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            List<string> expectedLabels = new List<string>
                {
                    "FakeLabel1",
                    "FakeLabel2"
                };

            if (AIServiceReturnsLabels)
            {
                foreach (string label in expectedLabels)
                {
                    mockGitHubEventClient.AILabelServiceReturn.Add(label);
                }
            }
            await IssueProcessing.InitialIssueTriage(mockGitHubEventClient, issueEventPayload);
            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");
            if (RuleState.On == ruleState)
            {
                Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");
                // Retrieve the IssueUpdate and verify the expected changes
                var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                if (AIServiceReturnsLabels)
                {
                    Assert.True(issueUpdate.Labels.Contains(LabelConstants.NeedsTeamTriage), $"IssueUpdate does not contain {LabelConstants.NeedsTeamTriage} which should have been added when labels were predicted.");
                    // Verify the labels returned by the AI service
                    foreach (string label in expectedLabels)
                    {
                        Assert.True(issueUpdate.Labels.Contains(label), $"IssueUpdate does not contain {label} which was returned by the AI service and should have been added.");
                    }
                }
                else
                {
                    // If the AI Label service doesn't predict labels, the label added is NeedsTriage
                    Assert.True(issueUpdate.Labels.Contains(LabelConstants.NeedsTriage), $"IssueUpdate does not contain {LabelConstants.NeedsTriage} which should have been added when no labels were predicted.");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// ManualIssueTriage requires 2 labeled event payloads to test. 
        /// 1. Labeled payload needs needs to be one where needs-triage is being added which should cause no change. 
        /// 2. Needs-triage needs to be already on the issues when another label is added. This should cause an update
        ///    where the needs-triage label is removed. This is also the payload used to check when the rule is Off.
        /// Trigger: issues labeled
        /// Conditions: Issue is open
        ///             Issue has "needs-triage" label
        ///             Label being added is NOT "needs-triage"
        /// Resulting Action: Remove "needs-triage" label
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <returns></returns>
        [TestCase(RulesConstants.ManualIssueTriage, "Tests.JsonEventPayloads/ManualIssueTriage_issue_labeled_needs-triage.json", RuleState.On, true)]
        [TestCase(RulesConstants.ManualIssueTriage, "Tests.JsonEventPayloads/ManualIssueTriage_issue_labeled_not_needs-triage.json", RuleState.On, false)]
        [TestCase(RulesConstants.ManualIssueTriage, "Tests.JsonEventPayloads/ManualIssueTriage_issue_labeled_not_needs-triage.json", RuleState.Off, false)]
        public async Task TestManualIssueTriage(string rule, string payloadFile, RuleState ruleState, bool labelAddedIsNeedsTriage)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            IssueProcessing.ManualIssueTriage(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // If the label being added is NeedsTriage, there should be no updates
                if (labelAddedIsNeedsTriage)
                {
                    Assert.AreEqual(0, totalUpdates, $"The label being added was {LabelConstants.NeedsTriage} and should not have produced any updates.");
                }
                else
                {
                    // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                    // Retrieve the IssueUpdate and verify the expected changes
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTriage} removed.");
                    // Verify that NeedsTriage was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTriage), $"IssueUpdate contains {LabelConstants.NeedsTriage} label which should have been removed.");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rule"></param>
        /// <param name="payloadFile"></param>
        /// <param name="ruleState"></param>
        /// <returns></returns>
        [NonParallelizable]
        [TestCase(RulesConstants.ServiceAttention, "Tests.JsonEventPayloads/ServiceAttention_issue_labeled.json", RuleState.Off, true)]
        [TestCase(RulesConstants.ServiceAttention, "Tests.JsonEventPayloads/ServiceAttention_issue_labeled.json", RuleState.On, true)]
        [TestCase(RulesConstants.ServiceAttention, "Tests.JsonEventPayloads/ServiceAttention_issue_labeled.json", RuleState.On, false)]

        public async Task TestServiceAttention(string rule, string payloadFile, RuleState ruleState, bool hasPartiesToMentionForServiceAttention)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);

            // Set the path to the fake CODEOWNERS file to be used for testing
            if (hasPartiesToMentionForServiceAttention)
            {
                CodeOwnerUtils.ResetCodeOwnerEntries();
                CodeOwnerUtils.codeOwnersFilePathOverride = "Tests.FakeCodeowners/ServiceAttention_has_CODEOWNERS";
            }
            else
            {
                CodeOwnerUtils.ResetCodeOwnerEntries();
                CodeOwnerUtils.codeOwnersFilePathOverride = "Tests.FakeCodeowners/ServiceAttention_does_not_have_CODEOWNERS";
            }
            IssueProcessing.ServiceAttention(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                if (hasPartiesToMentionForServiceAttention)
                {
                    string expectedNames = "@FakeUser1 @FakeUser11 @FakeUser4 @FakeUser14 @FakeUser24 @FakeUser9";
                    // "Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc @FakeUser1 @FakeUser11 @FakeUser4 @FakeUser14 @FakeUser24 @FakeUser9."
                    // There should be one update, a comment
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                    // Verify that a single comment was created
                    Assert.AreEqual(1, mockGitHubEventClient.GetComments().Count, $"{rule} should have produced a single comment.");
                    string comment = mockGitHubEventClient.GetComments()[0].Comment;
                    Assert.True(comment.Contains(expectedNames), $"Comment should have contained expected names {expectedNames} but did not. Full comment={comment}");
                }
                else
                {
                    Assert.AreEqual(0, totalUpdates, $"With no parties to mention for Service Attention, the rule should not have produced any updates.");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }


        /// <summary>
        /// Test CXPAttention rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issues labeled
        /// Conditions: Issue is open
        ///             Label being added is "CXP-Attention"
        ///             Does not have "Service-Attention" label
        /// Resulting Action: Add issues comment "Thank you for your feedback.  This has been routed to the support team for assistance."
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="hasServiceAttentionLabel">Where or not the payload has the Service Attention label</param>
        /// <returns></returns>
        [TestCase(RulesConstants.CXPAttention, "Tests.JsonEventPayloads/CXPAttention_issue_labeled.json", RuleState.Off, false)]
        [TestCase(RulesConstants.CXPAttention, "Tests.JsonEventPayloads/CXPAttention_issue_labeled.json", RuleState.On, false)]
        [TestCase(RulesConstants.CXPAttention, "Tests.JsonEventPayloads/CXPAttention_issue_labeled_has_service-attention.json", RuleState.Off, true)]

        public async Task TestCXPAttention(string rule, string payloadFile, RuleState ruleState, bool hasServiceAttentionLabel)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            IssueProcessing.CXPAttention(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // If the issues has the ServiceAttention label then there should be no updates
                if (hasServiceAttentionLabel)
                {
                    Assert.AreEqual(0, totalUpdates, $"Issue has {LabelConstants.ServiceAttention} and should not have produced any updates.");
                }
                else
                {
                    // There should be one comment created and no other updates
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");
                    // Verify that no issues update was produced
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNull(issueUpdate, $"{rule} should not have produced an IssueUpdate.");

                    // Verify that a single comment was created
                    Assert.AreEqual(1, mockGitHubEventClient.GetComments().Count, $"{rule} should have produced a single comment.");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test ManualTriageAfterExternalAssignment rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issue unlabeled
        /// Conditions: Issue is open
        ///             Has "customer-reported" label
        ///             Label removed is "Service Attention" OR "CXP Attention"
        /// Resulting Action: Add "needs-team-triage" label
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="alreadyHasNeedsTeamTriage">Whether or not the payload already has the needs-team-triage label</param>
        /// <returns></returns>
        [TestCase(RulesConstants.ManualTriageAfterExternalAssignment, "Tests.JsonEventPayloads/ManualTriageAfterExternalAssignment_issue_unlabeled_CXP_attention.json", RuleState.Off, false)]
        [TestCase(RulesConstants.ManualTriageAfterExternalAssignment, "Tests.JsonEventPayloads/ManualTriageAfterExternalAssignment_issue_unlabeled_CXP_attention.json", RuleState.On, false)]
        [TestCase(RulesConstants.ManualTriageAfterExternalAssignment, "Tests.JsonEventPayloads/ManualTriageAfterExternalAssignment_issue_unlabeled_service_attention.json", RuleState.Off, false)]
        [TestCase(RulesConstants.ManualTriageAfterExternalAssignment, "Tests.JsonEventPayloads/ManualTriageAfterExternalAssignment_issue_unlabeled_service_attention.json", RuleState.On, false)]
        [TestCase(RulesConstants.ManualTriageAfterExternalAssignment, "Tests.JsonEventPayloads/ManualTriageAfterExternalAssignment_issue_unlabeled_has_needs-team-triage.json", RuleState.On, true)]
        public async Task TestManualTriageAfterExternalAssignment(string rule, string payloadFile, RuleState ruleState, bool alreadyHasNeedsTeamTriage)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            IssueProcessing.ManualTriageAfterExternalAssignment(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // If issue already has needs-team-triage there should be no updates
                if (alreadyHasNeedsTeamTriage)
                {
                    Assert.AreEqual(0, totalUpdates, $"The issue already has {LabelConstants.NeedsTeamTriage} and should not have produced any updates.");
                }
                else
                {
                    // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");
                    // Retrieve the IssueUpdate and verify the expected changes
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTeamTriage} added.");
                    // Verify that NeedsTeamTriage was added
                    Assert.True(issueUpdate.Labels.Contains(LabelConstants.NeedsTeamTriage), $"IssueUpdate does not the {LabelConstants.NeedsTeamTriage} label which should have been added.");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test ResetIssueActivity rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issue reopened/edited
        /// Conditions: Issue is open OR Issue is being reopened
        ///             Issue has "no-recent-activity" label
        ///             User modifying the issue is NOT a known bot 
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <returns></returns>
        [TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_edited.json", RuleState.Off)]
        [TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_edited.json", RuleState.On)]
        [TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_reopened.json", RuleState.Off)]
        [TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_reopened.json", RuleState.On)]
        public async Task TestResetIssueActivity(string rule, string payloadFile, RuleState ruleState)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            IssueProcessing.ResetIssueActivity(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                // Retrieve the IssueUpdate and verify the expected changes
                var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NoRecentActivity} removed.");
                // Verify that NoRecentActivity was removed
                Assert.False(issueUpdate.Labels.Contains(LabelConstants.NoRecentActivity), $"IssueUpdate contains {LabelConstants.NoRecentActivity} label which should have been removed.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test RequireAttentionForNonMilestone rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issue labeled/unlabeled
        /// Conditions: Issue is open
        ///             Issue has label "customer-reported"
        ///             Issue does NOT have label "needs-team-attention"
        ///             Issue does NOT have label "needs-triage"
        ///             Issue does NOT have label "needs-team-triage"
        ///             Issue does NOT have label "needs-author-feedback"
        ///             Issue does NOT have label "issue-addressed"
        ///             Issue is not in a milestone
        /// Resulting Action: Add "needs-team-attention" label
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <returns></returns>
        [TestCase(RulesConstants.RequireAttentionForNonMilestone, "Tests.JsonEventPayloads/RequireAttentionForNonMilestone_issue_labeled.json", RuleState.Off)]
        [TestCase(RulesConstants.RequireAttentionForNonMilestone, "Tests.JsonEventPayloads/RequireAttentionForNonMilestone_issue_labeled.json", RuleState.On)]
        [TestCase(RulesConstants.RequireAttentionForNonMilestone, "Tests.JsonEventPayloads/RequireAttentionForNonMilestone_issue_unlabeled.json", RuleState.Off)]
        [TestCase(RulesConstants.RequireAttentionForNonMilestone, "Tests.JsonEventPayloads/RequireAttentionForNonMilestone_issue_unlabeled.json", RuleState.On)]
        public async Task TestRequireAttentionForNonMilestone(string rule, string payloadFile, RuleState ruleState)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            IssueProcessing.RequireAttentionForNonMilestone(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                // Retrieve the IssueUpdate and verify the expected changes
                var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTeamAttention} added.");
                // Verify that NeedsTeamAttention was added
                Assert.True(issueUpdate.Labels.Contains(LabelConstants.NeedsTeamAttention), $"IssueUpdate does not contain {LabelConstants.NeedsTeamAttention} label which should have been added.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test AuthorFeedbackNeeded rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Label added is "needs-author-feedback"
        /// Resulting Action: 
        ///             Remove "needs-triage" label
        ///             Remove "needs-team-triage" label
        ///             Remove "needs-team-attention" label
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="hasLabelsToRemove">Whether or not the payload Issue contains the labels that are being removed</param>
        /// <returns></returns>
        [TestCase(RulesConstants.AuthorFeedbackNeeded, "Tests.JsonEventPayloads/AuthorFeedbackNeeded_issue_labeled_with_labels_to_remove.json", RuleState.Off, true)]
        [TestCase(RulesConstants.AuthorFeedbackNeeded, "Tests.JsonEventPayloads/AuthorFeedbackNeeded_issue_labeled_with_labels_to_remove.json", RuleState.On, true)]
        [TestCase(RulesConstants.AuthorFeedbackNeeded, "Tests.JsonEventPayloads/AuthorFeedbackNeeded_issue_labeled_nothing_to_remove.json", RuleState.Off, false)]
        public async Task TestAuthorFeedbackNeeded(string rule, string payloadFile, RuleState ruleState, bool hasLabelsToRemove)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            IssueProcessing.AuthorFeedbackNeeded(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // If there are no labels to remove, there should be no updates
                if (!hasLabelsToRemove)
                {
                    Assert.AreEqual(0, totalUpdates, $"The label being removed, {LabelConstants.NeedsTriage}, was not on the Issue and should have not produced any updates.");
                }
                else
                {
                    // There should be one update, an IssueUpdate with the NeedsTriage, NeedsTeamTriage and NeedsTeamAttention labels removed.
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                    // Retrieve the IssueUpdate and verify the expected changes
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTriage} removed.");
                    // Verify that NeedsTriage, NeedsTeamTriage and NeedsTeamAttention were removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTriage), $"IssueUpdate contains {LabelConstants.NeedsTriage} label which should have been removed.");
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTeamTriage), $"IssueUpdate contains {LabelConstants.NeedsTeamTriage} label which should have been removed.");
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTeamAttention), $"IssueUpdate contains {LabelConstants.NeedsTeamAttention} label which should have been removed.");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test IssueAddressed rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Label added is "issue-addressed"
        /// Resulting Action: 
        ///     Remove "needs-triage" label
        ///     Remove "needs-team-triage" label
        ///     Remove "needs-team-attention" label
        ///     Remove "needs-author-feedback" label
        ///     Remove "no-recent-activity" label
        ///     Add issue comment
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="hasLabelsToRemove">Whether or not the payload Issue contains the labels that are being removed</param>
        /// <returns></returns>
        [TestCase(RulesConstants.IssueAddressed, "Tests.JsonEventPayloads/IssueAddressed_issue_labeled_with_labels_to_remove.json", RuleState.Off, true)]
        [TestCase(RulesConstants.IssueAddressed, "Tests.JsonEventPayloads/IssueAddressed_issue_labeled_with_labels_to_remove.json", RuleState.On, true)]
        [TestCase(RulesConstants.IssueAddressed, "Tests.JsonEventPayloads/IssueAddressed_issue_labeled_nothing_to_remove.json", RuleState.On, false)]
        public async Task TestIssueAddressed(string rule, string payloadFile, RuleState ruleState, bool hasLabelsToRemove)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            IssueProcessing.IssueAddressed(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // If the label being added is NeedsTriage, there should be no updates
                if (!hasLabelsToRemove)
                {
                    Assert.AreEqual(1, totalUpdates, $"With none of the labels to remove being on the Issue, there should still be 1 update from an added comment.");
                }
                else
                {
                    // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                    Assert.AreEqual(2, totalUpdates, $"The number of updates should have been 2 but was instead, {totalUpdates}");

                    // Retrieve the IssueUpdate and verify the expected changes
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTriage} removed.");

                    // Verify that NeedsTriage was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTriage), $"IssueUpdate contains {LabelConstants.NeedsTriage} label which should have been removed.");
                    // Verify that NeedsTeamTriage was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTeamTriage), $"IssueUpdate contains {LabelConstants.NeedsTeamTriage} label which should have been removed.");
                    // Verify that NeedsTeamAttention was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTeamAttention), $"IssueUpdate contains {LabelConstants.NeedsTeamAttention} label which should have been removed.");
                    // Verify that NeedsAuthorFeedback was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsAuthorFeedback), $"IssueUpdate contains {LabelConstants.NeedsAuthorFeedback} label which should have been removed.");
                    // Verify that NoRecentActivity was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NoRecentActivity), $"IssueUpdate contains {LabelConstants.NoRecentActivity} label which should have been removed.");
                }
                // Regardless of whether or not there were labels to remove, a single comment should be created.
                Assert.AreEqual(1, mockGitHubEventClient.GetComments().Count, $"{rule} should have produced a single comment.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }

        /// <summary>
        /// Test IssueAddressedReset rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Issue has label "issue-addressed"
        ///             Label added is any one of:
        ///                 "needs-team-attention"
        ///                 "needs-author-feedback"
        ///                 "Service Attention"
        ///                 "CXP Attention"
        ///                 "needs-triage"
        ///                 "needs-team-triage"
        /// Resulting Action: 
        ///     Remove "issue-addressed" label
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <returns></returns>
        [TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_CXP_attention.json", RuleState.Off)]
        [TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_CXP_attention.json", RuleState.On)]
        [TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_needs-author-feedack.json", RuleState.On)]
        [TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_needs-team-attention.json", RuleState.On)]
        [TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_needs-team-triage.json", RuleState.On)]
        [TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_needs-triage.json", RuleState.On)]
        [TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_service_attention.json", RuleState.On)]
        public async Task TestIssueAddressedReset(string rule, string payloadFile, RuleState ruleState)
        {
            var mockGitHubEventClient = new MockGitHubEventClient(OrgConstants.ProductHeaderName);
            mockGitHubEventClient.RulesConfiguration.Rules[rule] = ruleState;
            var rawJson = TestHelpers.GetTestEventPayload(payloadFile);
            var issueEventPayload = SimpleJsonSerializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            IssueProcessing.IssueAddressedReset(mockGitHubEventClient, issueEventPayload);

            // Verify the RuleCheck 
            Assert.AreEqual(ruleState == RuleState.On, mockGitHubEventClient.RulesConfiguration.RuleEnabled(rule), $"Rule '{rule}' enabled should have been {ruleState == RuleState.On} but RuleEnabled returned {ruleState != RuleState.On}.'");

            var totalUpdates = await mockGitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
            if (RuleState.On == ruleState)
            {
                // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                // Retrieve the IssueUpdate and verify the expected changes
                var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.IssueAddressed} removed.");
                // Verify that IssueAddressed was removed
                Assert.False(issueUpdate.Labels.Contains(LabelConstants.IssueAddressed), $"IssueUpdate contains {LabelConstants.IssueAddressed} label which should have been removed.");
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should not have produced any updates.");
            }
        }
    }
}
