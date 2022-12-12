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
        internal static async Task ProcessIssueEvent(GitHubClient gitHubClient, string rawJson)
        {
            var serializer = new SimpleJsonSerializer();
            IssueEventGitHubPayload issueEventPayload = serializer.Deserialize<IssueEventGitHubPayload>(rawJson);
            IssueUpdate issueUpdate = null;

            // Tasks cannot have a ref or out parameter, it needs to return the issueUpdate which will be whatever
            // was passed in, if there were no changes, or a created/updated one if there were.
            issueUpdate = await InitialIssueTriage(gitHubClient, issueEventPayload, issueUpdate);
            ManualIssueTriage(gitHubClient, issueEventPayload, ref issueUpdate);
            // adds a comment to the issue and doesn't use an issueUpdate
            await ServiceAttention(gitHubClient, issueEventPayload);
            // adds a comment to the issue and doesn't use an issueUpdate
            await CXPAttention(gitHubClient, issueEventPayload);
            // adds label and does use issueUpdate
            ManualTriageAfterExternalAssignment(gitHubClient, issueEventPayload, ref issueUpdate);

            // If any of the rules have made issueUpdate changes, it needs to be updated
            if (null != issueUpdate)
            {
                await gitHubClient.Issue.Update(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueUpdate);
            }

            // JRS - this is just a test to create an issue comment and verify the user with the @ is correct
            // This does work
            // string issueComment = $"Hi @{issueEventPayload.Issue.User.Login}.  Thank you for opening this issue and giving us the opportunity to assist.  We believe that this has been addressed.  If you feel that further discussion is needed, please add a comment with the text “`/unresolve`” to remove the “issue-addressed” label and continue the conversation.";
            // await gitHubClient.Issue.Comment.Create(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueComment);

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
        /// Resulting Action: JRS-TBD, the actions have their own conditions
        /// </summary>
        /// <param name="gitHubClient">Authenticated GitHubClient</param>
        /// <param name="issueEventPayload">Issue event payload</param>
        /// <param name="issueUpdate">The issue update object</param>
        /// <returns></returns>
        internal static async Task<IssueUpdate> InitialIssueTriage(GitHubClient gitHubClient, IssueEventGitHubPayload issueEventPayload, IssueUpdate issueUpdate)
        {
            // JRS-RulecCheck
            if (String.Equals(issueEventPayload.Action, ActionConstants.Opened))
            {
                // If there are no labels and no assignees
                if ((issueEventPayload.Issue.Labels.Count == 0) && (issueEventPayload.Issue.Assignee == null))
                {
                    // JRS - IF creator is NOT an Azure SDK team owner, not sure how to check this
                    bool isMember = await AuthUtils.IsUserMemberOfOrg(gitHubClient, OrgConstants.Azure, issueEventPayload.Sender.Login);
                    bool hasPermission = await AuthUtils.DoesUserHavePermissions(gitHubClient, issueEventPayload.Repository.Id, issueEventPayload.Sender.Login);
                    if (!isMember && !hasPermission)
                    {
                        if (null == issueUpdate)
                        {
                            issueUpdate = issueEventPayload.Issue.ToUpdate();
                        }
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
            if (String.Equals(issueEventPayload.Action, ActionConstants.Labeled))
            {
                // if the issue is open, has needs-triage label and label being added is not needs-triage
                // then remove the needs-triage label
                if (issueEventPayload.Issue.State == ItemState.Open &&
                    LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.NeedsTriage) &&
                    !issueEventPayload.Label.Name.Equals(LabelConstants.NeedsTriage))
                {
                    if (null == issueUpdate)
                    {
                        issueUpdate = issueEventPayload.Issue.ToUpdate();
                    }
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
            if (String.Equals(issueEventPayload.Action, ActionConstants.Labeled))
            {
                // JRS - what to do if ServiceAttention is the only label, there will be no
                // CodeOwnerEntries found?
                // if the issue is open, and the label being added is ServiceAttention
                if (issueEventPayload.Issue.State == ItemState.Open &&
                    issueEventPayload.Label.Name.Equals(LabelConstants.ServiceAttention))
                {
                    // JRS-CodeOwnersLocation??
                    CodeOwnerUtils.codeOwnersFilePathOverride = @"C:\src\azure-sdk-for-java\.github\CODEOWNERS";
                    string partiesToMention = CodeOwnerUtils.getPartiesToMentionForServiceAttention(issueEventPayload.Issue.Labels);
                    if (null != partiesToMention)
                    {
                        string issueComment = $"Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc ${partiesToMention}.";
                        await gitHubClient.Issue.Comment.Create(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueComment);
                    }
                    else
                    {
                        // JRS-What if there are no owners, just output the message?
                        // If there are no codeowners found output the issue URL
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
            if (String.Equals(issueEventPayload.Action, ActionConstants.Labeled))
            {

                if (issueEventPayload.Issue.State == ItemState.Open &&
                issueEventPayload.Label.Name.Equals(LabelConstants.CXPAttention) &&
                !LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.ServiceAttention))
                {
                    string issueComment = "Thank you for your feedback.  This has been routed to the support team for assistance.";
                    await gitHubClient.Issue.Comment.Create(issueEventPayload.Repository.Id, issueEventPayload.Issue.Number, issueComment);
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
            if (String.Equals(issueEventPayload.Action, ActionConstants.Unlabeled))
            {
                if (issueEventPayload.Issue.State == ItemState.Open &&
                LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.CustomerReported) &&
                (issueEventPayload.Label.Name.Equals(LabelConstants.CXPAttention) ||
                 issueEventPayload.Label.Name.Equals(LabelConstants.ServiceAttention)))
                {
                    if (null == issueUpdate)
                    {
                        issueUpdate = issueEventPayload.Issue.ToUpdate();
                    }
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
            Common_ResetIssueActivity(gitHubClient, issueEventPayload.Action, issueEventPayload.Issue, issueEventPayload.Issue.User, ref issueUpdate);
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
        /// <param name="gitHubClient"></param>
        /// <param name="Action"></param>
        /// <param name="user"></param>
        /// <param name="issueUpdate"></param>
        public static void Common_ResetIssueActivity(GitHubClient gitHubClient, string action, Issue issue, User user, ref IssueUpdate issueUpdate)
        {
            if ((issue.State == ItemState.Open || action == ActionConstants.Reopened) &&
                LabelUtils.HasLabel(issue.Labels, LabelConstants.NoRecentActivity) &&
                // If a user is a known GitHub bot, the user's AccountType will be Bot
                user.Type != AccountType.Bot)
            {
                if (null == issueUpdate) 
                { 
                    issueUpdate = issue.ToUpdate();
                }
                issueUpdate.AddLabel(LabelConstants.NeedsTeamTriage);
            }
        }
    }
}
