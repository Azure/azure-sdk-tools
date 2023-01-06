using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Octokit.Internal;
using Octokit;
using Azure.Sdk.Tools.GithubEventProcessor.GitHubAuth;
using System.Threading.Tasks;
using Azure.Sdk.Tools.GithubEventProcessor.GitHubPayload;
using Azure.Sdk.Tools.GithubEventProcessor.Constants;
using Azure.Sdk.Tools.GithubEventProcessor.Utils;
using static System.Collections.Specialized.BitVector32;
using System.Reflection.Emit;

namespace Azure.Sdk.Tools.GithubEventProcessor.EventProcessing
{
    internal class IssueProcessing
    {
        /// <summary>
        /// Issue rules can be found on the gist, https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#issue-rules
        /// Every rule will have it's own function that will be called here.
        /// </summary>
        /// <param name="gitHubClient"></param>
        /// <param name="rawJson"></param>
        /// <returns></returns>
        internal static async Task ProcessIssueEvent(GitHubClient gitHubClient, IssueEventGitHubPayload issueEventPayload)
        {
            IssueUpdate issueUpdate = null;

            // Tasks cannot have a ref or out parameter, it needs to return the issueUpdate which will be whatever
            // was passed in, if there were no changes, or a created/updated one if there were.
            issueUpdate = await InitialIssueTriage(gitHubClient, issueEventPayload, issueUpdate);
            ManualIssueTriage(gitHubClient, issueEventPayload, ref issueUpdate);
            // adds a comment to the issue and doesn't use an issueUpdate
            await ServiceAttention(gitHubClient, issueEventPayload);
            // adds a comment to the issue and doesn't use an issueUpdate
            await CXPAttention(gitHubClient, issueEventPayload);
            ManualTriageAfterExternalAssignment(gitHubClient, issueEventPayload, ref issueUpdate);
            RequireAttentionForNonMilestone(gitHubClient, issueEventPayload, ref issueUpdate);
            AuthorFeedbackNeeded(gitHubClient, issueEventPayload, ref issueUpdate);
            issueUpdate = await IssueAddressed(gitHubClient, issueEventPayload, issueUpdate);

            // If any of the rules have made issueUpdate changes, it needs to be updated
            if (null != issueUpdate)
            {
                await EventUtils.UpdateIssueOrPullRequest(gitHubClient, issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueUpdate);
            }
        }

        // Processing functions should always do the following, in order
        // 1. If the rule is based upon an Action (event), verify the rule action matches the action
        // 2. If #1 is true, verify all of the conditions
        // 3. If using an IssueUpdate, it'll be a ref parameter (if non-async), otherwise the funtion will
        //    return an updated IssueUpdate
        /// <summary>
        /// Initial Issue Triage https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#initial-issue-triage
        /// Trigger: issue opened
        /// Conditions: Issue has no labels
        ///             Issue has no assignee
        /// Resulting Action: JRS-TBD, I need the AI service to query
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueEventPayload">Issue event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        /// <returns></returns>
        internal static async Task<IssueUpdate> InitialIssueTriage(GitHubClient gitHubClient, IssueEventGitHubPayload issueEventPayload, IssueUpdate issueUpdate)
        {
            // JRS-RuleCheck
            if (issueEventPayload.Action == ActionConstants.Opened)
            {
                // If there are no labels and no assignees
                if ((issueEventPayload.Issue.Labels.Count == 0) && (issueEventPayload.Issue.Assignee == null))
                {
                    // JRS - IF creator is NOT an Azure SDK team owner - 
                    bool isMember = await AuthUtils.IsUserMemberOfOrg(gitHubClient, OrgConstants.Azure, issueEventPayload.Sender.Login);
                    bool isCollaborator = await AuthUtils.IsUserCollaborator(gitHubClient, issueEventPayload.Repository.Id, issueEventPayload.Sender.Login);
                    if (!isMember && !isCollaborator)
                    {
                        issueUpdate = EventUtils.GetIssueUpdate(issueEventPayload.Issue, issueUpdate);
                        issueUpdate.AddLabel(LabelConstants.NeedsTriage);
                    }
                }
                /* JRS The AI label service does not exist yet
                Query AI label service for suggestions:
                IF labels were predicted:
                    - Assign returned labels to the issue
                    - Add "needs-team-attention" label to the issue
                    IF service label is associated with an Azure SDK team member:
                        IF a single team member:
                            - Assign team member to the issue
                        ELSE
                            - Assign a random team member from the set to the issue
                            - Add a comment mentioning the other team members from the set
                        - Add comment indicating issue was routed for assistance  
                            (text: "Thank you for your feedback.  Tagging and routing to the team member best able to assist.")
                    ELSE
                        - Add "CXP Attention" label to the issue
                        - Create a comment mentioning (content from .NET rule #30)
                ELSE
                    - Add "needs-triage" label to the issue
                */
            }
            return issueUpdate;
        }

        /// <summary>
        /// Manual Issue Triage https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#manual-issue-triage
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Issue has "needs-triage" label
        ///             Label being added is NOT "needs-triage"
        /// Resulting Action: Remove "needs-triage" label
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueEventPayload">Issue event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        internal static void ManualIssueTriage(GitHubClient gitHubClient, IssueEventGitHubPayload issueEventPayload, ref IssueUpdate issueUpdate)
        {
            // JRS-RulecCheck
            if (issueEventPayload.Action == ActionConstants.Labeled)
            {
                // if the issue is open, has needs-triage label and label being added is not needs-triage
                // then remove the needs-triage label
                if (issueEventPayload.Issue.State == ItemState.Open &&
                    LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsTriage) &&
                    !issueEventPayload.Label.Name.Equals(LabelConstants.NeedsTriage))
                {
                    issueUpdate = EventUtils.GetIssueUpdate(issueEventPayload.Issue, issueUpdate);
                    issueUpdate.RemoveLabel(LabelConstants.NeedsTriage);
                }
            }
        }

        /// <summary>
        /// Service Attention https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#service-attention
        /// This does not use issue update, it creates a comment
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Label being added is "Service Attention"
        /// Resulting Action: Add issue comment
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueEventPayload">Issue event payload</param>
        /// <returns></returns>
        internal static async Task ServiceAttention(GitHubClient gitHubClient, IssueEventGitHubPayload issueEventPayload)
        {
            if (issueEventPayload.Action == ActionConstants.Labeled)
            {
                // JRS - what to do if ServiceAttention is the only label, there will be no
                // CodeOwnerEntries found?
                // if the issue is open, and the label being added is ServiceAttention
                if (issueEventPayload.Issue.State == ItemState.Open &&
                    issueEventPayload.Label.Name.Equals(LabelConstants.ServiceAttention))
                {
                    string partiesToMention = CodeOwnerUtils.getPartiesToMentionForServiceAttention(issueEventPayload.Issue.Labels);
                    if (null != partiesToMention)
                    {
                        string issueComment = $"Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc ${partiesToMention}.";
                        await EventUtils.CreateComment(gitHubClient, issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueComment);
                    }
                    else
                    {
                        // If there are no codeowners found then output the issue URL so it's in the logs for the event
                        Console.WriteLine($"There were no parties to mention for issue: {issueEventPayload.Issue.Url}");
                    }
                }
            }
        }

        /// <summary>
        /// CXP Attention https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#cxp-attention
        /// This does not use issue update, it creates a comment.
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Label being added is "CXP-Attention"
        ///             Does not have "Service-Attention" label
        /// Resulting Action: Add issue comment
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueEventPayload">Issue event payload</param>
        /// <returns></returns>
        internal static async Task CXPAttention(GitHubClient gitHubClient, IssueEventGitHubPayload issueEventPayload)
        {
            if (issueEventPayload.Action == ActionConstants.Labeled)
            {

                if (issueEventPayload.Issue.State == ItemState.Open &&
                issueEventPayload.Label.Name.Equals(LabelConstants.CXPAttention) &&
                !LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.ServiceAttention))
                {
                    string issueComment = "Thank you for your feedback.  This has been routed to the support team for assistance.";
                    await EventUtils.CreateComment(gitHubClient, issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueComment);
                }
            }
        }

        /// <summary>
        /// Manual Triage After External Assignment https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#manual-triage-after-external-assignment
        /// Trigger: issue unlabeled
        /// Conditions: Issue is open
        ///             Has "customer-reported" label
        ///             Label removed is "Service Attention" OR "CXP Attention"
        /// Resulting Action: Add "needs-team-triage" label
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueEventPayload">Issue event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        internal static void ManualTriageAfterExternalAssignment(GitHubClient gitHubClient, IssueEventGitHubPayload issueEventPayload, ref IssueUpdate issueUpdate)
        {
            if (issueEventPayload.Action == ActionConstants.Unlabeled)
            {
                if (issueEventPayload.Issue.State == ItemState.Open &&
                LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.CustomerReported) &&
                (issueEventPayload.Label.Name.Equals(LabelConstants.CXPAttention) ||
                 issueEventPayload.Label.Name.Equals(LabelConstants.ServiceAttention)))
                {
                    issueUpdate = EventUtils.GetIssueUpdate(issueEventPayload.Issue, issueUpdate);
                    issueUpdate.AddLabel(LabelConstants.NeedsTeamTriage);
                }
            }
        }

        /// <summary>
        /// Reset Issue Activity https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#reset-issue-activity
        /// See Common_ResetIssueActivity comments
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueEventPayload">Issue event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        internal static void ResetIssueActivity(GitHubClient gitHubClient, IssueEventGitHubPayload issueEventPayload, ref IssueUpdate issueUpdate)
        {
            Common_ResetIssueActivity(gitHubClient, issueEventPayload.Action, issueEventPayload.Issue, issueEventPayload.Sender, ref issueUpdate);
        }

        /// <summary>
        /// Common function for Reset Issue Activity
        /// Trigger: issue reopened/edited, issue_comment created
        /// Conditions: Issue is open OR Issue is being reopened
        ///             Issue has "no-recent-activity" label
        ///             User modifying the issue is NOT a known bot 
        /// Resulting Action: Add "needs-team-triage" label
        /// </summary>
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="action">The action being performed, from the payload object</param>
        /// <param name="user">Octokit.User object from the respective payload.</param>
        /// <param name="issueUpdate">The issue update object</param>
        public static void Common_ResetIssueActivity(GitHubClient gitHubClient, string action, Issue issue, User sender, ref IssueUpdate issueUpdate)
        {
            // Is this enabled?
            if ((issue.State == ItemState.Open || action == ActionConstants.Reopened) &&
                LabelUtils.HasLabel(issue.Labels, LabelConstants.NoRecentActivity) &&
                // If a user is a known GitHub bot, the user's AccountType will be Bot
                sender.Type != AccountType.Bot)
            {
                issueUpdate = EventUtils.GetIssueUpdate(issue, issueUpdate);
                issueUpdate.AddLabel(LabelConstants.NeedsTeamTriage);
            }
        }

        /// <summary>
        /// Require Attention For Non Milestone https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#require-attention-for-non-milestone
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
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueEventPayload">Issue event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        internal static void RequireAttentionForNonMilestone(GitHubClient gitHubClient, IssueEventGitHubPayload issueEventPayload, ref IssueUpdate issueUpdate)
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
                    issueUpdate = EventUtils.GetIssueUpdate(issueEventPayload.Issue, issueUpdate);
                    issueUpdate.AddLabel(LabelConstants.NeedsTeamAttention);
                }
            }
        }

        /// <summary>
        /// Author Feedback Needed https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#author-feedback-needed
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Label added is "needs-author-feedback"
        /// Resulting Action: 
        ///             Add "needs-team-attention" label
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueEventPayload">Issue event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        internal static void AuthorFeedbackNeeded(GitHubClient gitHubClient, IssueEventGitHubPayload issueEventPayload, ref IssueUpdate issueUpdate)
        {
            if (issueEventPayload.Action == ActionConstants.Labeled)
            {
                if (issueEventPayload.Issue.State == ItemState.Open &&
                    issueEventPayload.Label.Name == LabelConstants.NeedsAuthorFeedback)
                {
                    issueUpdate = EventUtils.GetIssueUpdate(issueEventPayload.Issue, issueUpdate);
                    issueUpdate.AddLabel(LabelConstants.NeedsTeamAttention);
                }
            }
        }
        // 
        /// <summary>
        /// Issue Addressed https://gist.github.com/jsquire/cfff24f50da0d5906829c5b3de661a84#issue-addressed
        /// Trigger: issue labeled
        /// Conditions: Issue is open
        ///             Label added is "needs-author-feedback"
        /// Resulting Action: 
        ///     Remove "needs-triage" label
        ///     Remove "needs-team-triage" label
        ///     Remove "needs-team-attention" label
        ///     Remove "needs-author-feedback" label
        ///     Remove "no-recent-activity" label
        ///     Add issue comment
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueEventPayload">Issue event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        internal static async Task<IssueUpdate> IssueAddressed(GitHubClient gitHubClient, IssueEventGitHubPayload issueEventPayload, IssueUpdate issueUpdate)
        {
            if (issueEventPayload.Action == ActionConstants.Labeled)
            {
                if (issueEventPayload.Issue.State == ItemState.Open &&
                    issueEventPayload.Label.Name == LabelConstants.NeedsAuthorFeedback)
                {
                    issueUpdate = EventUtils.GetIssueUpdate(issueEventPayload.Issue, issueUpdate);
                    issueUpdate.RemoveLabel(LabelConstants.NeedsTriage);
                    issueUpdate.RemoveLabel(LabelConstants.NeedsTeamAttention);
                    issueUpdate.RemoveLabel(LabelConstants.NeedsAuthorFeedback);
                    issueUpdate.RemoveLabel(LabelConstants.NoRecentActivity);
                    string issueComment = $"Hi {issueEventPayload.Issue.User.Login}.  Thank you for opening this issue and giving us the opportunity to assist.  We believe that this has been addressed.  If you feel that further discussion is needed, please add a comment with the text \"/unresolve\" to remove the \"issue-addressed\" label and continue the conversation.";
                    await EventUtils.CreateComment(gitHubClient, issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueComment);
                }
            }
            return issueUpdate;
        }
    }
}
