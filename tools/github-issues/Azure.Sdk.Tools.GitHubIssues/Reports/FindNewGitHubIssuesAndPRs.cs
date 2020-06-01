using Azure.Storage.Blobs;
using GitHubIssues.Helpers;
using GitHubIssues.Html;
using GitHubIssues.Models;
using Microsoft.Extensions.Logging;
using Octokit;
using OutputColorizer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GitHubIssues.Reports
{
    internal class FindNewGitHubIssuesAndPRs : BaseFunction
    {
        public FindNewGitHubIssuesAndPRs(ILogger log) : base(log)
        {

        }

#if DEBUG
        private static readonly string ContainerName = "lastaccessed-dev";
#else
        private static string ContainerName = "lastaccessed";
#endif

        public override void Execute()
        {
            _log.LogInformation($"Started function execution: {DateTime.Now}");

            var storageConnString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            BlobServiceClient bsc = new BlobServiceClient(storageConnString);
            BlobContainerClient bcc = bsc.GetBlobContainerClient(ContainerName);

            // create the container
            bcc.CreateIfNotExists();

            _log.LogInformation("Storage account accessed");

            DateTime to = DateTime.UtcNow;
            foreach (RepositoryConfig repositoryConfig in _cmdLine.RepositoriesList)
            {
                // retrieve the last accessed time for this repository
                BlobClient bc = bcc.GetBlobClient($"{repositoryConfig.Owner}_{repositoryConfig.Name}");
                DateTime lastDateRun = DateTime.UtcNow.AddDays(-1);

                try
                {
                    string content = StreamHelpers.GetContentAsString(bc.Download().Value.Content);
                    lastDateRun = DateTime.Parse(content);
                }
                catch
                {
                }

                _log.LogInformation("Last processed date for {0} is {1}", repositoryConfig, lastDateRun);

                string owner = repositoryConfig.Owner;
                string repo = repositoryConfig.Name;

                _log.LogInformation("Processing repository {0}\\{1}", owner, repo);

                HtmlPageCreator emailBody = new HtmlPageCreator($"New items in {repo}");

                // get new issues
                RetrieveNewItems(CreateQueryForNewItems(repositoryConfig, IssueIsQualifier.Issue), lastDateRun, emailBody, "New issues");

                // get new PRs
                RetrieveNewItems(CreateQueryForNewItems(repositoryConfig, IssueIsQualifier.PullRequest), lastDateRun, emailBody, "New PRs");

                // get needs attention issues
                RetrieveNeedsAttentionIssues(repositoryConfig, emailBody);

                emailBody.AddContent($"<p>Last checked range: {lastDateRun} -> {to} </p>");

                _log.LogInformation("Sending email...");
                // send the email
                EmailSender.SendEmail(_cmdLine.EmailToken, _cmdLine.FromEmail, emailBody.GetContent(), repositoryConfig.ToEmail, repositoryConfig.CcEmail, $"New issues in the {repo} repo as of {to.ToShortDateString()}", _log);

                _log.LogInformation("Email sent...");

                bc.Upload(StreamHelpers.GetStreamForString(to.ToUniversalTime().ToString()), overwrite: true);
                _log.LogInformation($"Persisted last event time for {repositoryConfig.Owner}\\{repositoryConfig.Name} as {to}");
            }
        }

        private bool RetrieveNewItems(SearchIssuesRequest requestOptions, DateTime from, HtmlPageCreator emailBody, string header)
        {
            TableCreator tc = new TableCreator(header);
            tc.DefineTableColumn("Title", TableCreator.Templates.Title);
            tc.DefineTableColumn("Labels", TableCreator.Templates.Labels);
            tc.DefineTableColumn("Author", TableCreator.Templates.Author);
            tc.DefineTableColumn("Assigned", TableCreator.Templates.Assigned);

            Colorizer.WriteLine("Retrieving issues");
            List<ReportIssue> issues = new List<ReportIssue>();
            foreach (Issue issue in _gitHub.SearchForGitHubIssues(requestOptions))
            {
                if (issue.CreatedAt.ToUniversalTime() >= from.ToUniversalTime())
                {
                    issues.Add(new ReportIssue() { Issue = issue, Note = string.Empty, Milestone = null });
                }
            }

            emailBody.AddContent(tc.GetContent(issues));

            return issues.Any();
        }

        private bool RetrieveNeedsAttentionIssues(RepositoryConfig repositoryConfig, HtmlPageCreator emailBody)
        {
            TableCreator tc = new TableCreator("Issues that need attention");
            tc.DefineTableColumn("Title", TableCreator.Templates.Title);
            tc.DefineTableColumn("Labels", TableCreator.Templates.Labels);
            tc.DefineTableColumn("Author", TableCreator.Templates.Author);
            tc.DefineTableColumn("Assigned", TableCreator.Templates.Assigned);

            List<ReportIssue> issues = new List<ReportIssue>();

            foreach (Issue issue in _gitHub.SearchForGitHubIssues(CreateQueryForNeedsAttentionItems(repositoryConfig)))
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

        private SearchIssuesRequest CreateQueryForNewItems(RepositoryConfig repoInfo, IssueIsQualifier issueType)
        {
            DateTime to = DateTime.UtcNow;
            DateTime fromTwoDaysBack = DateTime.UtcNow.AddDays(-2);

            SearchIssuesRequest requestOptions = new SearchIssuesRequest()
            {
#pragma warning disable CS0618 // Type or member is obsolete
                Created = DateRange.Between(fromTwoDaysBack, to),
#pragma warning restore CS0618 // Type or member is obsolete
                Order = SortDirection.Descending,
                Is = new[] { IssueIsQualifier.Open, issueType },
                Repos = new RepositoryCollection()
            };

            requestOptions.Repos.Add(repoInfo.Owner, repoInfo.Name);

            return requestOptions;
        }

        private SearchIssuesRequest CreateQueryForNeedsAttentionItems(RepositoryConfig repoInfo)
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
