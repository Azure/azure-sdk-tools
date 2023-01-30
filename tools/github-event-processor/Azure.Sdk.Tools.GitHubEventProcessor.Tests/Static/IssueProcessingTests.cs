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
        // JRS-TBD InitialIssueTriage test after the rule is finished. It's waiting on the AI service.

        /// <summary>
        /// ManualIssueTriage requires 2 labeled event payloads to test. 
        /// 1. Labeled payload needs needs to be one where needs-triage is being added which should cause no change. 
        /// 2. Needs-triage needs to be already on the issue when another label is added. This should cause an update
        ///    where the needs-triage label is removed. This is also the payload used to check when the rule is Off.
        /// Trigger: issue labeled
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
                    Assert.AreEqual(0, totalUpdates, $"The label being added was {LabelConstants.NeedsTriage} and should have produced any updates.");
                }
                else
                {
                    // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                    // Retrieve the IssueUpdate and verify the expected changes
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTriage} removed.");
                    // Verify that NeedsTriage was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTriage), $"IssueUpdate contains {LabelConstants.NeedsTriage} label which should have been removed");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should have produced any updates.");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
            return;
        }

        // JRS - has payload, rule needs to be written, requires a custom CODEOWNERS for test
        //[TestCase(RulesConstants.ServiceAttention, "Tests.JsonEventPayloads/ServiceAttention_issue_labeled_service-attention.json", RuleState.Off, false)]

        public async Task TestServiceAttention(string rule, string payloadFile, RuleState ruleState, bool labelAddedIsNeedsTriage)
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
                    Assert.AreEqual(0, totalUpdates, $"The label being added was {LabelConstants.NeedsTriage} and should have produced any updates.");
                }
                else
                {
                    // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                    // Retrieve the IssueUpdate and verify the expected changes
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTriage} removed.");
                    // Verify that NeedsTriage was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTriage), $"IssueUpdate contains {LabelConstants.NeedsTriage} label which should have been removed");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should have produced any updates.");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
            return;
        }


        /// <summary>
        /// Test CXPAttention rule enabled/disabled, with a payload that would cause updates when enabled.
        /// Verify all the expected updates when enabled and no updates when disabled.
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Label being added is "CXP-Attention"
        ///             Does not have "Service-Attention" label
        /// Resulting Action: Add issue comment "Thank you for your feedback.  This has been routed to the support team for assistance."
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
                // If the issue has the ServiceAttention label then there should be no updates
                if (hasServiceAttentionLabel)
                {
                    Assert.AreEqual(0, totalUpdates, $"Issue has {LabelConstants.ServiceAttention} and should have produced any updates.");
                }
                else
                {
                    // There should be one comment created and no other updates
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");
                    // Verify that no issue update was produced
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNull(issueUpdate, $"{rule} should not have produced an IssueUpdate.");

                    // Verify that a single comment was created
                    Assert.AreEqual(1, mockGitHubEventClient.GetComments().Count, $"{rule} should have produced a single comment.");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should have produced any updates.");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
            return;
        }

        // JRS - has payload, rule needs to be written
        /// <summary>
        /// 
        /// </summary>
        /// <param name="rule">String, RulesConstants for the rule being tested</param>
        /// <param name="payloadFile">JSon payload file for the event being tested</param>
        /// <param name="ruleState">Whether or not the rule is on/off</param>
        /// <param name="hasNeedsTeamTriageLabel"></param>
        /// <returns></returns>
        //[TestCase(RulesConstants.ManualTriageAfterExternalAssignment, "Tests.JsonEventPayloads/ManualTriageAfterExternalAssignment_issue_unlabeled_CXP_attention.json", RuleState.Off, false)]
        //[TestCase(RulesConstants.ManualTriageAfterExternalAssignment, "Tests.JsonEventPayloads/ManualTriageAfterExternalAssignment_issue_unlabeled_service_attention.json", RuleState.Off, false)]
        //[TestCase(RulesConstants.ManualTriageAfterExternalAssignment, "Tests.JsonEventPayloads/ManualTriageAfterExternalAssignment_issue_unlabeled_has_needs-team-triage.json", RuleState.Off, false)]
        public async Task TestManualTriageAfterExternalAssignment(string rule, string payloadFile, RuleState ruleState, bool hasServiceAttentionLabel)
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
                if (hasServiceAttentionLabel)
                {
                    Assert.AreEqual(0, totalUpdates, $"The label being added was {LabelConstants.NeedsTriage} and should have produced any updates.");
                }
                else
                {
                    // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                    // Retrieve the IssueUpdate and verify the expected changes
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTriage} removed.");
                    // Verify that NeedsTriage was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTriage), $"IssueUpdate contains {LabelConstants.NeedsTriage} label which should have been removed");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should have produced any updates.");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
            return;
        }

        // JRS - has payload, rule needs to be written
        //[TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_edited.json", RuleState.Off, false)]
        //[TestCase(RulesConstants.ResetIssueActivity, "Tests.JsonEventPayloads/ResetIssueActivity_issue_reopened.json", RuleState.Off, false)]
        public async Task TestResetIssueActivity(string rule, string payloadFile, RuleState ruleState, bool labelAddedIsNeedsTriage)
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
                    Assert.AreEqual(0, totalUpdates, $"The label being added was {LabelConstants.NeedsTriage} and should have produced any updates.");
                }
                else
                {
                    // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                    // Retrieve the IssueUpdate and verify the expected changes
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTriage} removed.");
                    // Verify that NeedsTriage was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTriage), $"IssueUpdate contains {LabelConstants.NeedsTriage} label which should have been removed");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should have produced any updates.");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
            return;
        }

        // JRS - has payload, rule needs to be written
        //[TestCase(RulesConstants.RequireAttentionForNonMilestone, "Tests.JsonEventPayloads/RequireAttentionForNonMilestone_issue_labeled.json", RuleState.Off, false)]
        //[TestCase(RulesConstants.RequireAttentionForNonMilestone, "Tests.JsonEventPayloads/RequireAttentionForNonMilestone_issue_unlabeled.json", RuleState.Off, false)]
        public async Task TestRequireAttentionForNonMilestone(string rule, string payloadFile, RuleState ruleState, bool labelAddedIsNeedsTriage)
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
                    Assert.AreEqual(0, totalUpdates, $"The label being added was {LabelConstants.NeedsTriage} and should have produced any updates.");
                }
                else
                {
                    // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                    // Retrieve the IssueUpdate and verify the expected changes
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTriage} removed.");
                    // Verify that NeedsTriage was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTriage), $"IssueUpdate contains {LabelConstants.NeedsTriage} label which should have been removed");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should have produced any updates.");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
            return;
        }

        // JRS - has payload, rule needs to be written
        //[TestCase(RulesConstants.AuthorFeedbackNeeded, "Tests.JsonEventPayloads/AuthorFeedBack_issue_comment_created.json", RuleState.Off, false)]
        //[TestCase(RulesConstants.AuthorFeedbackNeeded, "Tests.JsonEventPayloads/AuthorFeedbackNeeded_issue_labeled_nothing_to_remove.json", RuleState.Off, false)]
        public async Task TestAuthorFeedbackNeeded(string rule, string payloadFile, RuleState ruleState, bool labelAddedIsNeedsTriage)
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
                    Assert.AreEqual(0, totalUpdates, $"The label being added was {LabelConstants.NeedsTriage} and should have produced any updates.");
                }
                else
                {
                    // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                    // Retrieve the IssueUpdate and verify the expected changes
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTriage} removed.");
                    // Verify that NeedsTriage was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTriage), $"IssueUpdate contains {LabelConstants.NeedsTriage} label which should have been removed");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should have produced any updates.");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
            return;
        }

        // JRS - has payload, rule needs to be written
        //[TestCase(RulesConstants.IssueAddressed, "Tests.JsonEventPayloads/IssueAddressed_issue_labeled.json", RuleState.Off, false)]
        public async Task TestIssueAddressed(string rule, string payloadFile, RuleState ruleState, bool labelAddedIsNeedsTriage)
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
                    Assert.AreEqual(0, totalUpdates, $"The label being added was {LabelConstants.NeedsTriage} and should have produced any updates.");
                }
                else
                {
                    // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                    // Retrieve the IssueUpdate and verify the expected changes
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTriage} removed.");
                    // Verify that NeedsTriage was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTriage), $"IssueUpdate contains {LabelConstants.NeedsTriage} label which should have been removed");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should have produced any updates.");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
            return;
        }

        // JRS - has payload, rule needs to be written
        //[TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_needs-author-feedack.json", RuleState.Off, false)]
        //[TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_needs-team-attention.json", RuleState.Off, false)]
        //[TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_needs-team-triage.json", RuleState.Off, false)]
        //[TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_needs-triage.json", RuleState.Off, false)]
        //[TestCase(RulesConstants.IssueAddressedReset, "Tests.JsonEventPayloads/IssueAddressedReset_issue_labeled_no-recent-activity.json", RuleState.Off, false)]
        public async Task TestIssueAddressedReset(string rule, string payloadFile, RuleState ruleState, bool labelAddedIsNeedsTriage)
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
                    Assert.AreEqual(0, totalUpdates, $"The label being added was {LabelConstants.NeedsTriage} and should have produced any updates.");
                }
                else
                {
                    // There should be one update, an IssueUpdate with the NoRecentActivity label removed
                    Assert.AreEqual(1, totalUpdates, $"The number of updates should have been 1 but was instead, {totalUpdates}");

                    // Retrieve the IssueUpdate and verify the expected changes
                    var issueUpdate = mockGitHubEventClient.GetIssueUpdate();
                    Assert.IsNotNull(issueUpdate, $"{rule} is {ruleState} and should have produced an IssueUpdate with {LabelConstants.NeedsTriage} removed.");
                    // Verify that NeedsTriage was removed
                    Assert.False(issueUpdate.Labels.Contains(LabelConstants.NeedsTriage), $"IssueUpdate contains {LabelConstants.NeedsTriage} label which should have been removed");
                }
            }
            else
            {
                Assert.AreEqual(0, totalUpdates, $"{rule} is {ruleState} and should have produced any updates.");
                Assert.IsNull(mockGitHubEventClient.GetIssueUpdate(), $"{rule} is {ruleState} and should not have produced an IssueUpdate.");
            }
            return;
        }
    }
}
