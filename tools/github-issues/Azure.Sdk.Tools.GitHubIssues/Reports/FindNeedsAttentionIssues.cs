using GitHubIssues.Helpers;
using GitHubIssues.Html;
using GitHubIssues.Models;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GitHubIssues.Reports
{
    internal class FindNeedsAttentionIssues : BaseFunction
    {
        public FindNeedsAttentionIssues(ILogger log) : base(log)
        {

        }

        public override void Execute()
        {
            foreach (RepositoryConfig repositoryConfig in _cmdLine.RepositoriesList)
            {
                _log.LogInformation($"Retrieve issues marked with {Constants.Labels.NeedsAttention} for repository {repositoryConfig.Name}");

                HtmlPageCreator emailBody = new HtmlPageCreator($"Issues that need attention in {repositoryConfig.Name}");

                bool hasFoundIssues = RetrieveNeedsAttentionIssues(repositoryConfig, emailBody);

                if (hasFoundIssues)
                {
                    // send the email
                    EmailSender.SendEmail(_cmdLine.EmailToken, _cmdLine.FromEmail, emailBody.GetContent(), repositoryConfig.ToEmail, repositoryConfig.CcEmail,
                        $"Issues that need attention in {repositoryConfig.Name}", _log);
                }
            }
        }

        private bool RetrieveNeedsAttentionIssues(RepositoryConfig repositoryConfig, HtmlPageCreator emailBody)
        {
            TableCreator tc = new TableCreator("Issues that need attention");
            tc.DefineTableColumn("Title", TableCreator.Templates.Title);
            tc.DefineTableColumn("Labels", TableCreator.Templates.Labels);
            tc.DefineTableColumn("Author", TableCreator.Templates.Author);
            tc.DefineTableColumn("Assigned", TableCreator.Templates.Assigned);

            List<ReportIssue> issues = new List<ReportIssue>();

            foreach (Issue issue in _gitHub.SearchForGitHubIssues(CreateQuery(repositoryConfig)))
            {
                issues.Add(new ReportIssue()
                {
                    Issue = issue,
                    Milestone = issue.Milestone,
                    Note = string.Empty
                });
            }

            emailBody.AddContent(tc.GetContent(issues));
            return issues.Any();
        }

        private SearchIssuesRequest CreateQuery(RepositoryConfig repoInfo)
        {
            // Find all open issues
            //  That are marked with 'needs-attention'

            SearchIssuesRequest requestOptions = new SearchIssuesRequest()
            {
                Repos = new RepositoryCollection(),
                Labels = new string[] { Constants.Labels.NeedsAttention },
                Is = new[] { IssueIsQualifier.Issue },
                State = ItemState.Open
            };

            requestOptions.Repos.Add(repoInfo.Owner, repoInfo.Name);

            return requestOptions;
        }
    }
}
