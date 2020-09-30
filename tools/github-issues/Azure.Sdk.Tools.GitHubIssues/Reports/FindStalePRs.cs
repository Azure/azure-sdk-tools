using Azure.Sdk.Tools.GitHubIssues.Email;
using Azure.Sdk.Tools.GitHubIssues.Services.Configuration;
using Azure.Security.KeyVault.Secrets;
using GitHubIssues.Helpers;
using GitHubIssues.Html;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubIssues.Reports
{
    public class FindStalePRs : BaseReport
    {
        public FindStalePRs(IConfigurationService configurationService, ILogger<FindStalePRs> logger) : base(configurationService, logger)
        {

        }

        protected override async Task ExecuteCoreAsync(ReportExecutionContext context)
        {
            foreach (RepositoryConfiguration repositoryConfiguration in context.RepositoryConfigurations)
            {
                await ExecuteWithRetryAsync(3, async () =>
                {
                    HtmlPageCreator emailBody = new HtmlPageCreator($"Pull Requests older than 3 months in {repositoryConfiguration.Name}");
                    bool hasFoundPRs = FindStalePRsInRepo(context, repositoryConfiguration, emailBody);

                    if (hasFoundPRs)
                    {
                        // send the email
                        EmailSender.SendEmail(context.SendGridToken, context.FromAddress, emailBody.GetContent(), repositoryConfiguration.ToEmail, repositoryConfiguration.CcEmail,
                            $"Pull Requests older than 3 months in {repositoryConfiguration.Name}", Logger);
                    }
                });
            }
        }

        private bool FindStalePRsInRepo(ReportExecutionContext context, RepositoryConfiguration repositoryConfig, HtmlPageCreator emailBody)
        {
            TableCreator tc = new TableCreator($"Pull Requests older than {DateTime.Now.AddMonths(-3).ToShortDateString()}");
            tc.DefineTableColumn("Title", TableCreator.Templates.Title);
            tc.DefineTableColumn("Labels", TableCreator.Templates.Labels);
            tc.DefineTableColumn("Author", TableCreator.Templates.Author);
            tc.DefineTableColumn("Assigned", TableCreator.Templates.Assigned);

            Logger.LogInformation($"Retrieving PR information for repo {repositoryConfig.Name}");
            List<ReportIssue> oldPrs = new List<ReportIssue>();
            foreach (Issue issue in context.GitHubClient.SearchForGitHubIssues(CreateQuery(repositoryConfig)))
            {
                Logger.LogInformation($"Found stale PR  {issue.Number}");
                oldPrs.Add(new ReportIssue() { Issue = issue, Note = string.Empty, Milestone = null });
            }

            emailBody.AddContent(tc.GetContent(oldPrs));
            return oldPrs.Any();
        }

        private SearchIssuesRequest CreateQuery(RepositoryConfiguration repoInfo)
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
