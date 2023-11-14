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
using System.Linq;
using System.Security;
using Azure.Sdk.Tools.CodeownersUtils.Verification;
using System.Runtime.Intrinsics.X86;

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
        /// Initial Issue Triage
        /// Trigger: issue opened
        /// Conditions: Issue has no labels
        ///             Issue has no assignee
        /// Resulting Actions:
        ///    Query AI label service for label suggestions:
        ///      IF labels were predicted:
        ///        - Assign returned labels to the issue
        ///    	  IF service and category labels have AzureSdkOwners (in CODEOWNERS):
        ///         IF a single AzureSdkOwner:
        ///           - Assign the AzureSdkOwner issue
        ///         ELSE
        ///           - Assign a random AzureSdkOwner from the set to the issue
        ///           - Create the following comment, mentioning all AzureSdkOwners from the set
        ///             "@{person1} @{person2}...${personX}"
        ///       - Create the following comment
        ///         "Thank you for your feedback.  Tagging and routing to the team member best able to assist."
        ///
        ///       # Note: No valid AzureSdkOwners means there were no CODEOWNERS entries for the ServiceLabel OR no
        ///       # CODEOWNERS entries for the ServiceLabel with AzureSdkOwners OR there is a CODEOWNERS entry with
        ///       # AzureSdkOwners but none of them have permissions to be assigned to an issue for the repository. 
        ///       IF there are no valid AzureSdkOwners, but there are ServiceOwners, and the ServiceAttention rule is enabled
        ///         - Add "Service Attention" label to the issue and apply the logic from the "Service Attention" rule
        ///       ELSE
        ///         - Add "needs-team-triage" (at this point it owners cannot be determined for this issue)
        ///
        ///       IF "needs-team-triage" is not being added to the issue
        ///         - Add "needs-team-attention" label to the issue
        ///
        ///
        ///      Evaluate the user that created the issue:
        ///         IF the user is NOT a member of the Azure Org
        ///           IF the user does not have Admin or Write Collaborator permission
        ///             - Add "customer-reported" label
        ///             - Add "question" label
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
                        List<string> labelSuggestions = await gitHubEventClient.QueryAILabelService(issueEventPayload);
                        if (labelSuggestions.Count > 0)
                        {
                            // needs-team-attention needs to be added if it can be determined who this issue actually
                            // belongs to.
                            // If labels were predicted, add them to the issue
                            foreach (string label in labelSuggestions)
                            {
                                gitHubEventClient.AddLabel(label);
                            }

                            // Uncomment this when CODEOWNERS are unfubar'd
                            //bool addNeedsTeamAttention = true;

                            //var azureSdkOwners = CodeOwnerUtils.GetAzureSdkOwnersForServiceLabels(labelSuggestions);
                            //bool hasValidAssignee = false;
                            //if (azureSdkOwners.Count > 0)
                            //{
                            //    // If there's only a single owner, 
                            //    if (azureSdkOwners.Count == 1)
                            //    {
                            //        if (await gitHubEventClient.OwnerCanBeAssignedToIssuesInRepo(
                            //                                        issueEventPayload.Repository.Owner.Login,
                            //                                        issueEventPayload.Repository.Name,
                            //                                        azureSdkOwners[0]))
                            //        {
                            //            hasValidAssignee = true;
                            //            gitHubEventClient.AssignOwnerToIssue(
                            //                                issueEventPayload.Repository.Owner.Login,
                            //                                issueEventPayload.Repository.Name,
                            //                                azureSdkOwners[0]);
                            //        }
                            //        // Output something into the logs pointing out that AzureSdkOwners has a user that cannot
                            //        // be assigned to an issue
                            //        else
                            //        {
                            //            Console.WriteLine($"{azureSdkOwners[0]} is the only owner in the AzureSdkOwners for service label(s), {string.Join(",", labelSuggestions)}, but cannot be assigned as an issue owner in this repository.");
                            //        }
                            //    }
                            //    // else there are multiple owners and a random one needs to be assigned
                            //    else
                            //    {
                            //        // Create a list of AzureSdkOwners that has been randomized. The reason
                            //        // the entire list is being randomed is because each person has to be
                            //        // checked to see if they can be assigned to an issue in the repository.
                            //        // and having the entire list being random simplifies processing if a given
                            //        // owner cannot be assigned.
                            //        var rnd = new Random();
                            //        var randomAzureSdkOwners = azureSdkOwners.OrderBy(item => rnd.Next(0, azureSdkOwners.Count));
                            //        foreach (string azureSdkOwner in randomAzureSdkOwners)
                            //        {
                            //            if (await gitHubEventClient.OwnerCanBeAssignedToIssuesInRepo(
                            //                                            issueEventPayload.Repository.Owner.Login,
                            //                                            issueEventPayload.Repository.Name,
                            //                                            azureSdkOwner))
                            //            {
                            //                hasValidAssignee = true;
                            //                gitHubEventClient.AssignOwnerToIssue(issueEventPayload.Repository.Owner.Login,
                            //                                                     issueEventPayload.Repository.Name,
                            //                                                     azureSdkOwner);
                            //                // As soon as there's a valid assignee, add the comment mentioning everyone
                            //                // in the AzureSdkOwners and exit. The @ mention is only necessary if there
                            //                // are multiple AzureSdkOwners.
                            //                string azureSdkOwnersAtMention = CodeOwnerUtils.CreateAtMentionForOwnerList(azureSdkOwners);
                            //                gitHubEventClient.CreateComment(issueEventPayload.Repository.Id, 
                            //                                                issueEventPayload.Issue.Number, 
                            //                                                azureSdkOwnersAtMention);
                            //                break;
                            //            }
                            //            else
                            //            {
                            //                Console.WriteLine($"{azureSdkOwner} is an AzureSdkOwner for service labels {string.Join(",", labelSuggestions)} but cannot be assigned as an issue owner in this repository.");
                            //            }
                            //        }
                            //    }
                            //    // If the issue had a valid assignee add the comment
                            //    if (hasValidAssignee)
                            //    {
                            //        string issueComment = "Thank you for your feedback. Tagging and routing to the team member best able to assist.";
                            //        gitHubEventClient.CreateComment(issueEventPayload.Repository.Id,
                            //                                        issueEventPayload.Issue.Number,
                            //                                        issueComment);
                            //    }
                            //    else
                            //    {
                            //        // Output a message indicating every owner in the AzureSdkOwners, for the AI label suggestions. The lines immediately
                            //        // above this output will contain the messages for each user checked.
                            //        Console.WriteLine($"AzureSdkOwners for service labels {string.Join(",", labelSuggestions)} has no owners that can be assigned to issues in this repository.");
                            //    }
                            //}

                            //// If there's no valid AzureSdkOwner to assign the issue to (this means that there's either
                            //// no AzureSdkOwners or none of them have permissions to be assigned to an issue)
                            //if (!hasValidAssignee)
                            //{
                            //    // Get the list of ServiceOwners, if any.
                            //    var serviceOwners = CodeOwnerUtils.GetServiceOwnersForServiceLabels(labelSuggestions);

                            //    // Check to see if there are ServiceOwners and the ServiceAttention rule is turned on. If
                            //    // both are true then add the ServiceAttention label and run ServiceAttention processing
                            //    if (serviceOwners.Count > 0
                            //        && gitHubEventClient.RulesConfiguration.RuleEnabled(RulesConstants.ServiceAttention,
                            //                                                            false /* don't output log messages for this check*/))

                            //    {
                            //        gitHubEventClient.AddLabel(LabelConstants.ServiceAttention);
                            //        Common_ProcessServiceAttentionForLabels(gitHubEventClient,
                            //                                                issueEventPayload.Issue,
                            //                                                issueEventPayload.Repository.Id,
                            //                                                labelSuggestions);
                            //    }
                            //    // At this point, it cannot be determined who this issue belongs to. Add
                            //    // the needs-team-triage label instead of the needs-team-attention label
                            //    else
                            //    {
                            //        gitHubEventClient.AddLabel(LabelConstants.NeedsTeamTriage);
                            //        addNeedsTeamAttention = false;
                            //    }
                            //}

                            //// The needs-team-attention label is only added when it can be determined
                            //// who this issue belongs to.
                            //if (addNeedsTeamAttention)
                            //{
                            //    gitHubEventClient.AddLabel(LabelConstants.NeedsTeamAttention);
                            //}
                            gitHubEventClient.AddLabel(LabelConstants.NeedsTeamTriage);

                        }
                        // If there are no labels predicted add NeedsTriage to the issue
                        else
                        {
                            gitHubEventClient.AddLabel(LabelConstants.NeedsTriage);
                        }

                        // If the user is not a member of the Azure Org AND the user does not have write or admin collaborator permission.
                        // This piece is executed for every issue created that doesn't have labels or owners on it at the time of creation.
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
                    // If the issue is open
                    if (issueEventPayload.Issue.State == ItemState.Open)
                    {
                        // If ServiceAttention was the label added then for each label already on the issue that isn't
                        // ServiceAttention find the people to @ mention
                        if (issueEventPayload.Label.Name.Equals(LabelConstants.ServiceAttention))
                        {
                            // Before bothering to fetch parties to mention from the CodeOwners file, ensure that ServiceAttention
                            // isn't the only label on the issue.
                            if (issueEventPayload.Issue.Labels.Count > 1)
                            {
                                Common_ProcessServiceAttentionForLabels(gitHubEventClient, issueEventPayload);
                            }
                            else
                            {
                                Console.WriteLine($"{LabelConstants.ServiceAttention} is the only label on the issue. Other labels are required in order to get parties to mention from the Codeowners file.");
                            }
                        }
                        // Else the label being added is not ServiceAttention and ServiceAttention is already on the issue
                        // but only for the label added
                        else if (!issueEventPayload.Label.Name.Equals(LabelConstants.ServiceAttention) &&
                                 LabelUtils.HasLabel(issueEventPayload.Issue.Labels, LabelConstants.ServiceAttention))
                        {
                            Common_ProcessServiceAttentionForLabels(gitHubEventClient,
                                                                    issueEventPayload.Issue,
                                                                    issueEventPayload.Repository.Id,
                                                                    new List<string> { issueEventPayload.Label.Name });
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
        /// Overloaded function to call the common function after converting the IReadOnlyList of Octokit.Label
        /// into a list of string that only contains the label
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issueEventPayload">IssueEventGitHubPayload deserialized from the json event payload</param>
        public static void Common_ProcessServiceAttentionForLabels(GitHubEventClient gitHubEventClient, IssueEventGitHubPayload issueEventPayload)
        {
            List<string> labelOnlyList = issueEventPayload.Issue.Labels.Select(l => l.Name).ToList();
            Common_ProcessServiceAttentionForLabels(gitHubEventClient, issueEventPayload.Issue, issueEventPayload.Repository.Id, labelOnlyList);
        }

        /// <summary>
        /// This is an odd one. The Service Attention is being changed to add @ mentions when Service Attention is added
        /// OR when a label is added and Service Attention already exists OR, in the initial issue processing when
        /// ServiceAttention. Sanity says this common function needs to check if ServiceAttention is enabled but
        /// whether or not to call the function (aka, the event action and criteria) still need to be done by the calling
        /// method). This differs from the other common functions because the individual rules need to process things differently
        /// before getting to this point. Some will get the labels from the issue whereas others are adding labels and wanting
        /// ServiceAttention processing.
        /// </summary>
        /// <param name="gitHubEventClient">Authenticated GitHubEventClient</param>
        /// <param name="issue">Octokit.Issue object from the respective payload.</param>
        /// <param name="repositoryId">Long, the ID of the repository.</param>
        /// <param name="labels">The list of labels to look for service owners for.</param>
        public static void Common_ProcessServiceAttentionForLabels(GitHubEventClient gitHubEventClient, Issue issue, long repositoryId, List<string> labels)
        {
            if (labels.Count > 0)
            {
                List<string> ownersToMention = CodeOwnerUtils.GetServiceOwnersForServiceLabels(labels);
                if (ownersToMention.Count > 0)
                {
                    string partiesToMention = CodeOwnerUtils.CreateAtMentionForOwnerList(ownersToMention);
                    string issueComment = $"Thanks for the feedback! We are routing this to the appropriate team for follow-up. cc {partiesToMention}.";
                    gitHubEventClient.CreateComment(repositoryId, issue.Number, issueComment);
                }
                else
                {
                    // If there were no Service Owners found for the list of labels, output the URL and the list
                    // of labels
                    Console.WriteLine($"There were no parties to mention found for the following labels ({string.Join(",", labels)}) for issue: {issue.Url}");
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
