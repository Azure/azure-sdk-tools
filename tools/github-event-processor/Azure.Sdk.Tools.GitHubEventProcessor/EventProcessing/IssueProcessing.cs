using System;
using Octokit;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GitHubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GitHubEventProcessor.Constants;
using Azure.Sdk.Tools.GitHubEventProcessor.Utils;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using static System.Net.Mime.MediaTypeNames;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Xml.Linq;
using System.Collections.Generic;

namespace Azure.Sdk.Tools.GitHubEventProcessor.EventProcessing
{
    public class IssueProcessing
    {
        /// <summary>
        /// Every rule will have it's own function that will be called here, the rule configuration will determine
        /// which rules will execute.
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issueEventPayload">IssueEventGitHubPayload deserialized from the json event payload</param>
        public static async Task ProcessIssueEvent(GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {
            await InitialIssueTriage(gitHubEventClient, issueEventPayload);
            ManualIssueTriage(gitHubEventClient, issueEventPayload);
            ServiceAttention(gitHubEventClient, issueEventPayload);
            ManualTriageAfterExternalAssignment(gitHubEventClient, issueEventPayload);
            RequireAttentionForNonMilestone(gitHubEventClient, issueEventPayload);
            AuthorFeedbackNeeded(gitHubEventClient, issueEventPayload);
            IssueAddressed(gitHubEventClient, issueEventPayload);
            IssueAddressedReset(gitHubEventClient, issueEventPayload);

            // After all of the rules have been processed, call to process pending updates
            await gitHubEventClient.ProcessPendingUpdates(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number);
        }

        /// <summary>
        /// Note: This function does not yet process the way the rule was written because that requires information
        /// we do not yet have. The proposal is to update CODEOWNERS which is still TBD. When that's updated, this
        /// rule will need to be updated accordingly. The issue tracking this is
        /// https://github.com/Azure/azure-sdk-tools/issues/5743
        /// Initial Issue Triage
        /// Trigger: issue opened
        /// Conditions: Issue has no labels
        ///             Issue has no assignee
        /// Resulting Actions:
        ///     Evaluate the user that created the issue:
        ///         IF creator is NOT an Azure SDK team owner
        ///           IF the user is NOT a member of the Azure Org
        ///             IF the user does not have Admin or Write Collaborator permission
        ///               Add "customer-reported" label
        ///               Add "question" label
        ///     Query AI label service for label suggestions:
        ///     IF labels were predicted:
        ///         Assign returned labels to the issue
        ///         Add "needs-team-attention" label to the issue
        ///         IF service label is associated with an Azure SDK team member:
        ///             IF a single team member:
        ///                 Assign team member to the issue
        ///             ELSE
        ///                 Assign a random team member from the set to the issue
        ///                 Add a comment mentioning the other team members from the set
        ///                 Add comment indicating issue was routed for assistance: "Thank you for your feedback.  Tagging and routing to the team member best able to assist."
        ///         ELSE (service label is not associated with an Azure SDK team member)
        ///             Add "CXP Attention" label to the issue
        ///             Create a comment mentioning(content from .NET rule #30)
        ///     ELSE (labels were not predicted)
        ///         Add "needs-triage" label to the issue
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issueEventPayload">IssueEventGitHubPayload deserialized from the json event payload</param>
        public static async Task InitialIssueTriage(GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.InitialIssueTriage))
            {
                if (issueEventPayload.Action == ActionConstants.Opened)
                {
                    // If there are no labels and no assignees
                    if ((issueEventPayload.Issue.Labels.Count == 0) && (issueEventPayload.Issue.Assignee == null))
                    {
                        // This is a stop-gap. To do what is above, we need CODEOWNERS changes which don't exist yet.
                        // For the moment, if there are no label suggestions add needs-triage and if there
                        // are, add them and then add needs-team-triage. The issue created to track these future updates
                        // is https://github.com/Azure/azure-sdk-tools/issues/5743
                        List<string> labelSuggestions = await gitHubEventClient.QueryAILabelService(issueEventPayload);
                        if (labelSuggestions.Count > 0 )
                        {
                            foreach (string label in labelSuggestions)
                            {
                                gitHubEventClient.AddLabel(label);
                            }
                            gitHubEventClient.AddLabel(LabelConstants.NeedsTeamTriage);
                        }
                        else
                        {
                            gitHubEventClient.AddLabel(LabelConstants.NeedsTriage);
                        }

                        // If the user is not a member of the Azure Org AND the user does not have write or admin collaborator permission
                        bool isMemberOfOrg = await gitHubEventClient.IsUserMemberOfOrg(OrgConstants.Azure, issueEventPayload.Issue.User.Login);
                        if (!isMemberOfOrg)
                        {
                            bool hasAdminOrWritePermission = await gitHubEventClient.DoesUserHaveAdminOrWritePermission(issueEventPayload.Repository.Id, issueEventPayload.Issue.User.Login);
                            if (!hasAdminOrWritePermission)
                            {
                                gitHubEventClient.AddLabel(LabelConstants.CustomerReported);
                                gitHubEventClient.AddLabel(LabelConstants.Question);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Manual Issue Triage
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Issue has "needs-triage" label
        ///             Label being added is NOT "needs-triage"
        /// Resulting Action: Remove "needs-triage" label
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issueEventPayload">IssueEventGitHubPayload deserialized from the json event payload</param>
        public static void ManualIssueTriage(GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.ManualIssueTriage))
            {
                if (issueEventPayload.Action == ActionConstants.Labeled)
                {
                    // if the issue is open, has needs-triage label and label being added is not needs-triage
                    // then remove the needs-triage label
                    if (issueEventPayload.Issue.State == ItemState.Open &&
                        LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsTriage) &&
                        !issueEventPayload.Label.Name.Equals(LabelConstants.NeedsTriage))
                    {
                        gitHubEventClient.RemoveLabel(LabelConstants.NeedsTriage);
                    }
                }
            }
        }

        /// <summary>
        /// Service Attention
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Label being added is "Service Attention"
        /// Resulting Action: Add issue comment "Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc ${mentionees}."
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issueEventPayload">IssueEventGitHubPayload deserialized from the json event payload</param>
        public static void ServiceAttention(GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.ServiceAttention))
            {
                if (issueEventPayload.Action == ActionConstants.Labeled)
                {
                    // If the issue is open, and the label being added is ServiceAttention
                    if (issueEventPayload.Issue.State == ItemState.Open &&
                        issueEventPayload.Label.Name.Equals(LabelConstants.ServiceAttention))
                    {
                        // Before bothering to fetch parties to mention from the CodeOwners file, ensure that ServiceAttention
                        // isn't the only label on the issue.
                        if (issueEventPayload.Issue.Labels.Count > 1)
                        {
                            string partiesToMention = CodeOwnerUtils.GetPartiesToMentionForServiceAttention(issueEventPayload.Issue.Labels);
                            if (null != partiesToMention)
                            {
                                string issueComment = $"Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc {partiesToMention}.";
                                gitHubEventClient.CreateComment(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueComment);
                            }
                            else
                            {
                                // If there are no codeowners found then output the issue URL so it's in the logs for the event
                                Console.WriteLine($"There were no parties to mention for issue: {issueEventPayload.Issue.Url}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"{LabelConstants.ServiceAttention} is the only label on the issue. Other labels are required in order to get parties to mention from the Codeowners file.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Manual Triage After External Assignment
        /// Trigger: issue unlabeled
        /// Conditions: Issue is open
        ///             Issue is unassigned
        ///             Has "customer-reported" label
        ///             Label removed is "Service Attention"
        /// Resulting Action: Add "needs-team-triage" label
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issueEventPayload">IssueEventGitHubPayload deserialized from the json event payload</param>
        public static void ManualTriageAfterExternalAssignment(GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.ManualTriageAfterExternalAssignment))
            {
                if (issueEventPayload.Action == ActionConstants.Unlabeled)
                {
                    if (issueEventPayload.Issue.State == ItemState.Open &&
                        issueEventPayload.Issue.Assignee == null &&
                        issueEventPayload.Label.Name.Equals(LabelConstants.ServiceAttention) &&
                        LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.CustomerReported) &&
                        !LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsTeamTriage))
                    {
                        gitHubEventClient.AddLabel(LabelConstants.NeedsTeamTriage);
                    }
                }
            }
        }

        /// <summary>
        /// Reset Issue Activity
        /// For issue, the trigger is Edited or Reopened
        /// See Common_ResetIssueActivity comments
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issueEventPayload">IssueEventGitHubPayload deserialized from the json event payload</param>
        public static void ResetIssueActivity(GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {
            if (issueEventPayload.Action == ActionConstants.Edited || issueEventPayload.Action == ActionConstants.Reopened)
            {
                // The rules check is in the common function
                Common_ResetIssueActivity(gitHubEventClient, issueEventPayload.Action, issueEventPayload.Issue, issueEventPayload.Sender);
            }
        }

        /// <summary>
        /// Common function for Reset Issue Activity
        /// Trigger: issue reopened/edited, issue_comment created
        /// Conditions: Issue is open OR Issue is being reopened
        ///             Issue has "no-recent-activity" label
        ///             User modifying the issue is NOT a known bot
        /// Resulting Action: Remove "no-recent-activity" label
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="action">The action being performed, from the payload object</param>
        /// <param name="issue">Octokit.Issue object from the respective payload.</param>
        /// <param name="sender">Octokit.User from the payload's Sender.</param>
        public static void Common_ResetIssueActivity(GitHubEventClient gitHubEventClient, string action, Issue issue, User sender)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.ResetIssueActivity))
            {
                if ((issue.State == ItemState.Open || action == ActionConstants.Reopened) &&
                    LabelUtils.HasLabel(issue.Labels, LabelConstants.NoRecentActivity) &&
                    // If a user is a known GitHub bot, the user's AccountType will be Bot
                    sender.Type != AccountType.Bot)
                {
                    gitHubEventClient.RemoveLabel(LabelConstants.NoRecentActivity);
                }
            }
        }

        /// <summary>
        /// Require Attention For Non Milestone
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
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issueEventPayload">IssueEventGitHubPayload deserialized from the json event payload</param>
        public static void RequireAttentionForNonMilestone(GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.RequireAttentionForNonMilestone))
            {
                if (issueEventPayload.Action == ActionConstants.Labeled || issueEventPayload.Action == ActionConstants.Unlabeled)
                {
                    if (issueEventPayload.Issue.State == ItemState.Open &&
                        LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.CustomerReported) &&
                        !LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsTeamAttention) &&
                        !LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsTriage) &&
                        !LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsTeamTriage) &&
                        !LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsAuthorFeedback) &&
                        !LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.IssueAddressed) &&
                        null == issueEventPayload.Issue.Milestone)
                    {
                        gitHubEventClient.AddLabel(LabelConstants.NeedsTeamAttention);
                    }
                }
            }
        }

        /// <summary>
        /// Author Feedback Needed
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Label added is "needs-author-feedback"
        /// Resulting Action:
        ///             Remove "needs-triage" label
        ///             Remove "needs-team-triage" label
        ///             Remove "needs-team-attention" label
        ///             Create the following comment
        ///             "Hi @{issueAuthor}. Thank you for opening this issue and giving us the opportunity to assist. To help our
        ///             team better understand your issue and the details of your scenario please provide a response to the question
        ///             asked above or the information requested above. This will help us more accurately address your issue."
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issueEventPayload">IssueEventGitHubPayload deserialized from the json event payload</param>
        public static void AuthorFeedbackNeeded(GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.AuthorFeedbackNeeded))
            {
                if (issueEventPayload.Action == ActionConstants.Labeled)
                {
                    if (issueEventPayload.Issue.State == ItemState.Open &&
                        issueEventPayload.Label.Name == LabelConstants.NeedsAuthorFeedback)
                    {
                        // Any of these labels will be removed if they exist on the Issue. If none exist then
                        // then the comment will be the only update
                        if (LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsTriage))
                        {
                            gitHubEventClient.RemoveLabel(LabelConstants.NeedsTriage);
                        }
                        if (LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsTeamTriage))
                        {
                            gitHubEventClient.RemoveLabel(LabelConstants.NeedsTeamTriage);
                        }
                        if (LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsTeamAttention))
                        {
                            gitHubEventClient.RemoveLabel(LabelConstants.NeedsTeamAttention);
                        }
                        string issueComment = $"Hi @{issueEventPayload.Issue.User.Login}. Thank you for opening this issue and giving us the opportunity to assist. To help our team better understand your issue and the details of your scenario please provide a response to the question asked above or the information requested above. This will help us more accurately address your issue.";
                        gitHubEventClient.CreateComment(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueComment);
                    }
                }
            }
        }

        /// <summary>
        /// Issue Addressed
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Label added is "issue-addressed"
        /// Resulting Action:
        ///     Remove "needs-triage" label if it exists on the issue
        ///     Remove "needs-team-triage" label if it exists on the issue
        ///     Remove "needs-team-attention" label if it exists on the issue
        ///     Remove "needs-author-feedback" label if it exists on the issue
        ///     Remove "no-recent-activity" label if it exists on the issue
        ///     Add issue comment
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issueEventPayload">IssueEventGitHubPayload deserialized from the json event payload</param>
        public static void IssueAddressed(GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.IssueAddressed))
            {
                if (issueEventPayload.Action == ActionConstants.Labeled)
                {
                    if (issueEventPayload.Issue.State == ItemState.Open &&
                        issueEventPayload.Label.Name == LabelConstants.IssueAddressed)
                    {
                        if (LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsTriage))
                        {
                            gitHubEventClient.RemoveLabel(LabelConstants.NeedsTriage);
                        }
                        if (LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsTeamTriage))
                        {
                            gitHubEventClient.RemoveLabel(LabelConstants.NeedsTeamTriage);
                        }
                        if (LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsTeamAttention))
                        {
                            gitHubEventClient.RemoveLabel(LabelConstants.NeedsTeamAttention);
                        }
                        if (LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsAuthorFeedback))
                        {
                            gitHubEventClient.RemoveLabel(LabelConstants.NeedsAuthorFeedback);
                        }
                        if (LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NoRecentActivity))
                        {
                            gitHubEventClient.RemoveLabel(LabelConstants.NoRecentActivity);
                        }
                        // The comment is always created
                        string issueComment = $"Hi @{issueEventPayload.Issue.User.Login}.  Thank you for opening this issue and giving us the opportunity to assist.  We believe that this has been addressed.  If you feel that further discussion is needed, please add a comment with the text \"/unresolve\" to remove the \"issue-addressed\" label and continue the conversation.";
                        gitHubEventClient.CreateComment(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueComment);
                    }
                }
            }
        }

        /// <summary>
        /// Issue Addressed Reset
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
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issueEventPayload">IssueEventGitHubPayload deserialized from the json event payload</param>
        public static void IssueAddressedReset(GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {
            if (gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.IssueAddressedReset))
            {
                if (issueEventPayload.Action == ActionConstants.Labeled)
                {
                    if (issueEventPayload.Issue.State == ItemState.Open &&
                        LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.IssueAddressed))
                    {
                        if (issueEventPayload.Label.Name == LabelConstants.NeedsTeamAttention ||
                            issueEventPayload.Label.Name == LabelConstants.NeedsAuthorFeedback ||
                            issueEventPayload.Label.Name == LabelConstants.ServiceAttention ||
                            issueEventPayload.Label.Name == LabelConstants.NeedsTriage ||
                            issueEventPayload.Label.Name == LabelConstants.NeedsTeamTriage)
                        {
                            gitHubEventClient.RemoveLabel(LabelConstants.IssueAddressed);
                        }
                    }
                }
            }
        }
    }
}
