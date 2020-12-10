using Azure.Sdk.Tools.GitHubIssues.Email;
using Azure.Sdk.Tools.GitHubIssues.Services.Configuration;
using GitHubIssues.Helpers;
using GitHubIssues.Html;
using Microsoft.Extensions.Logging;
using Octokit;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubIssues.Reports
{
    public partial class FindCustomerRelatedIssuesInvalidState : BaseReport
    {
        public FindCustomerRelatedIssuesInvalidState(IConfigurationService configurationService, ILogger<FindCustomerRelatedIssuesInvalidState> logger) : base(configurationService, logger)
        {

        }

        protected override async Task ExecuteCoreAsync(ReportExecutionContext context)
        {
            foreach (RepositoryConfiguration repositoryConfiguration in context.RepositoryConfigurations)
            {
                await ExecuteWithRetryAsync(3, async () =>
                {
                    HtmlPageCreator emailBody = new HtmlPageCreator($"Customer reported issues with invalid state in {repositoryConfiguration.Name}");
                    bool hasFoundIssues = ValidateCustomerReportedIssues(context, repositoryConfiguration, emailBody);

                    if (hasFoundIssues)
                    {
                        // send the email
                        EmailSender.SendEmail(context.SendGridToken, context.FromAddress, emailBody.GetContent(), repositoryConfiguration.ToEmail, repositoryConfiguration.CcEmail,
                            $"Customer reported issues in invalid state in repo {repositoryConfiguration.Name}", Logger);
                    }
                });
            }
        }

        private bool ValidateCustomerReportedIssues(ReportExecutionContext context, RepositoryConfiguration repositoryConfig, HtmlPageCreator emailBody)
        {
            TableCreator tc = new TableCreator("Customer reported issues");
            tc.DefineTableColumn("Title", TableCreator.Templates.Title);
            tc.DefineTableColumn("Labels", TableCreator.Templates.Labels);
            tc.DefineTableColumn("Author", TableCreator.Templates.Author);
            tc.DefineTableColumn("Assigned", TableCreator.Templates.Assigned);
            tc.DefineTableColumn("Issues Found", i => i.Note);

            List<ReportIssue> issuesWithNotes = new List<ReportIssue>();

            SearchIssuesRequest searchRequest = CreateQuery(repositoryConfig);
            searchRequest.Type = IssueTypeQualifier.Issue;

            foreach (Issue issue in context.GitHubClient.SearchForGitHubIssues(searchRequest))
            {
                if (!ValidateIssue(issue, out string issuesFound))
                {
                    issuesWithNotes.Add(new ReportIssue() { Issue = issue, Note = issuesFound });
                    Logger.LogWarning($"{issue.Number}: {issuesFound}");
                }
            }

            // order the issues by the list of issues they have.
            issuesWithNotes = issuesWithNotes.OrderBy(i => i.Note).ToList();

            emailBody.AddContent(tc.GetContent(issuesWithNotes));

            return issuesWithNotes.Any();
        }

        private bool ValidateIssue(Issue issue, out string issuesFound)
        {
            // This validated that an issue has the common fields correctly setup
            // The issue has only one of the 'bug', 'feature-request' and 'question'
            // The issue has an owner assigned to it (if not in the backlog milestone)
            // The issue has the appropriate service-level labels
            //   - Service attention requires a service label to be set
            //   - If 'Service' is set, service attention is required
            // The issue has the appropriate milestone set
            //   - A question cannot be in the backlog milestone
            //   - A bug/feature-request need to have a valid milestone associated with them

            StringBuilder problemsWithTheIssue = new StringBuilder();
            bool isValidIssue = true;

            isValidIssue &= ValidateIssueType(issue, problemsWithTheIssue);
            isValidIssue &= ValidateIssueAssignee(issue, problemsWithTheIssue);
            isValidIssue &= ValidateServiceLabels(issue, problemsWithTheIssue);
            isValidIssue &= ValidateMilestone(issue, problemsWithTheIssue);

            issuesFound = problemsWithTheIssue.ToString();
            return isValidIssue;
        }

        private bool ValidateIssueType(Issue issue, StringBuilder problemsWithTheIssue)
        {
            IssueType issueType = GetIssueType(issue.Labels);

            // The issue should have one of the 'bug', 'feature-request' or 'question' label
            if ((issueType & (issueType - 1)) != 0)
            {
                problemsWithTheIssue.Append("The issue must have **just** one of the 'bug', 'feature-request' or 'question' labels. ");
                return false;
            }

            // the issue should have at least one of the 'bug', 'feature-request' and 'question'
            if (issueType == IssueType.None)
            {
                problemsWithTheIssue.Append("The issue must have one of the 'bug', 'feature-request' or 'question' labels. ");
                return false;
            }

            return true;
        }

        private bool ValidateIssueAssignee(Issue issue, StringBuilder problemsWithTheIssue)
        {
            // if the issue is not assigned to anyone AND
            // the issue is NOT in the backlog milestone (a milestone with no dueOn date)
            if (issue.Assignee == null && issue.Milestone != null && issue.Milestone.DueOn != null)
            {
                problemsWithTheIssue.Append("The issue must be assigned to an owner. ");
                return false;
            }

            return true;
        }

        private bool ValidateServiceLabels(Issue issue, StringBuilder problemsWithTheIssue)
        {
            //   - Service attention requires a service label to be set
            //   - If 'Service' is set, service attention is required

            ImpactArea impactedArea = GetImpactArea(issue.Labels);
            // has service attention
            bool hasServiceAttentionLabel = issue.Labels.Any(i => StringComparer.OrdinalIgnoreCase.Equals(i.Name, Constants.Labels.ServiceAttention));

            if (!hasServiceAttentionLabel && impactedArea.HasFlag(ImpactArea.Service))
            {
                problemsWithTheIssue.Append("The Azure SDK team does not own any issues in the Service. ");
                return false;
            }

            bool hasServiceLabel = issue.Labels.Any(i => i.Color == "e99695" && i.Name != Constants.Labels.ServiceAttention);

            // check to see if it has a service label if service attention is set
            if (hasServiceAttentionLabel && !hasServiceLabel)
            {
                problemsWithTheIssue.Append("The issue needs a service label. ");
                return false;
            }

            // check to see if the issue has an ownership (one of Client, Mgmt, Service)
            if (impactedArea == ImpactArea.None)
            {
                problemsWithTheIssue.Append("The issue needs an impacted area (i.e. Client, Mgmt or Service). ");
                return false;
            }

            // the issue should not have more than 1 type of labels to indicate the type.
            if ((impactedArea & (impactedArea - 1)) != 0)
            {
                problemsWithTheIssue.Append("The impacted area must have just one of the 'Client', 'Mgmt', 'Service', 'EngSys' and 'EngSys-Mgmt' labels. ");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that the milestone is correctly specified.
        /// </summary>
        /// <param name="issue"></param>
        /// <param name="problemsWithTheIssue"></param>
        /// <returns></returns>
        private bool ValidateMilestone(Issue issue, StringBuilder problemsWithTheIssue)
        {
            Milestone issueMilestone = issue.Milestone;
            IssueType issueType = GetIssueType(issue.Labels);

            if (issueType == IssueType.None)
            {
                // There is nothing to say here because we don't know if this is a bug of question at this point.
                return false;
            }

            // If the milestone is not set and we don't have a question
            if (issueMilestone == null && issueType != IssueType.Question)
            {
                problemsWithTheIssue.Append("A 'bug' or 'feature-request' must be assigned to a milestone. ");
                return false;
            }

            // If we are not looking at a question
            if (issueType != IssueType.Question)
            {
                // Is the milestone closed?
                if (issue.Milestone.State == ItemState.Closed)
                {
                    problemsWithTheIssue.Append("The issue must be assigned to an active milestone. ");
                    return false;
                }
                else
                {
                    // Is the milestone's due-date in the past?
                    if (issue.Milestone.DueOn != null && issue.Milestone.DueOn < DateTimeOffset.Now)
                    {
                        problemsWithTheIssue.Append("The issue must be assigned to an active milestone that is not past due. ");
                        return false;
                    }
                }
            }
            else
            {
                // for questions we should make sure they are not parked in the backlog milestone
                if (issue.Milestone != null && issue.Milestone.DueOn == null)
                {
                    problemsWithTheIssue.Append("A 'question' should not be assigned to a milestone without an end-date. ");
                    return false;
                }
            }

            return true;
        }

        private IssueType GetIssueType(IReadOnlyList<Label> labels)
        {
            IssueType type = IssueType.None;
            foreach (Label label in labels)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(label.Name, Constants.Labels.Bug))
                {
                    type |= IssueType.Bug;
                }
                if (StringComparer.OrdinalIgnoreCase.Equals(label.Name, Constants.Labels.Feature))
                {
                    type |= IssueType.Feature;
                }
                if (StringComparer.OrdinalIgnoreCase.Equals(label.Name, Constants.Labels.Question))
                {
                    type |= IssueType.Question;
                }
            }
            return type;
        }

        private ImpactArea GetImpactArea(IReadOnlyList<Label> labels)
        {
            ImpactArea area = ImpactArea.None;
            foreach (Label label in labels)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(label.Name, Constants.Labels.Client))
                {
                    area |= ImpactArea.Client;
                }
                if (StringComparer.OrdinalIgnoreCase.Equals(label.Name, Constants.Labels.Mgmt))
                {
                    area |= ImpactArea.Mgmt;
                }
                if (StringComparer.OrdinalIgnoreCase.Equals(label.Name, Constants.Labels.Service))
                {
                    area |= ImpactArea.Service;
                }
                if (StringComparer.OrdinalIgnoreCase.Equals(label.Name, Constants.Labels.EngSys))
                {
                    area |= ImpactArea.EngSys;
                }
                if (StringComparer.OrdinalIgnoreCase.Equals(label.Name, Constants.Labels.MgmtEngSys))
                {
                    area |= ImpactArea.MgmtEngSys;
                }
            }
            return area;
        }

        private SearchIssuesRequest CreateQuery(RepositoryConfiguration repoInfo)
        {
            // Find all open issues
            //  That are marked as customer reported

            SearchIssuesRequest requestOptions = new SearchIssuesRequest()
            {
                Repos = new RepositoryCollection(),
                Labels = new string[] { Constants.Labels.CustomerReported },
                State = ItemState.Open
            };

            requestOptions.Repos.Add(repoInfo.Owner, repoInfo.Name);

            return requestOptions;
        }
    }
}
