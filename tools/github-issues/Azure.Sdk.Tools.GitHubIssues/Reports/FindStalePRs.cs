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
    internal class FindStalePRs : BaseFunction
    {
        public FindStalePRs(ILogger log) : base(log)
        {

        }

        protected override void ExecuteCore()
        {
            foreach (RepositoryConfig repositoryConfig in _cmdLine.RepositoriesList)
            {
                HtmlPageCreator emailBody = new HtmlPageCreator($"Pull Requests older than 3 months in {repositoryConfig.Name}");
                bool hasFoundPRs = FindStalePRsInRepo(repositoryConfig, emailBody);

                if (hasFoundPRs)
                {
                    // send the email
                    EmailSender.SendEmail(_cmdLine.EmailToken, _cmdLine.FromEmail, emailBody.GetContent(), repositoryConfig.ToEmail, repositoryConfig.CcEmail,
                        $"Pull Requests older than 3 months in {repositoryConfig.Name}", _log);
                }
            }
        }

        private bool FindStalePRsInRepo(RepositoryConfig repositoryConfig, HtmlPageCreator emailBody)
        {
            TableCreator tc = new TableCreator($"Pull Requests older than {DateTime.Now.AddMonths(-3).ToShortDateString()}");
            tc.DefineTableColumn("Title", TableCreator.Templates.Title);
            tc.DefineTableColumn("Labels", TableCreator.Templates.Labels);
            tc.DefineTableColumn("Author", TableCreator.Templates.Author);
            tc.DefineTableColumn("Assigned", TableCreator.Templates.Assigned);

            _log.LogInformation($"Retrieving PR information for repo {repositoryConfig.Name}");
            List<ReportIssue> oldPrs = new List<ReportIssue>();
            foreach (Issue issue in _gitHub.SearchForGitHubIssues(CreateQuery(repositoryConfig)))
            {
                _log.LogInformation($"Found stale PR  {issue.Number}");
                oldPrs.Add(new ReportIssue() { Issue = issue, Note = string.Empty, Milestone = null });
            }

            emailBody.AddContent(tc.GetContent(oldPrs));
            return oldPrs.Any();
        }

        private SearchIssuesRequest CreateQuery(RepositoryConfig repoInfo)
        {
            // Find all open PRs
            //  That were created more than 3 months ago

            SearchIssuesRequest requestOptions = new SearchIssuesRequest()
            {
                Repos = new RepositoryCollection(),
                State = ItemState.Open,
#pragma warning disable CS0618 // Type or member is obsolete
                Created = DateRange.LessThanOrEquals(DateTime.Now.AddMonths(-3)),
#pragma warning restore CS0618 // Type or member is obsolete
                Order = SortDirection.Ascending,
                Is = new[] { IssueIsQualifier.PullRequest }
            };

            requestOptions.Repos.Add(repoInfo.Owner, repoInfo.Name);

            return requestOptions;
        }
    }
}
