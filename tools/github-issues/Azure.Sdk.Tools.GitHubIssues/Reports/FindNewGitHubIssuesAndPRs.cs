using Azure.Sdk.Tools.GitHubIssues.Email;
using Azure.Sdk.Tools.GitHubIssues.Services.Configuration;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using GitHubIssues.Helpers;
using GitHubIssues.Html;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubIssues.Reports
{
    public class FindNewGitHubIssuesAndPRs : BaseReport
    {
        public FindNewGitHubIssuesAndPRs(IConfigurationService configurationService, ILogger<FindNewGitHubIssuesAndPRs> logger) : base(configurationService, logger)
        {

        }

#if DEBUG
        private static readonly string ContainerName = "lastaccessed-dev";
#else
        private static string ContainerName = "lastaccessed";
#endif

        protected override async Task ExecuteCoreAsync(ReportExecutionContext context)
        {
            Logger.LogInformation($"Started function execution: {DateTime.Now}");

            var storageConnString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            BlobServiceClient bsc = new BlobServiceClient(storageConnString);
            BlobContainerClient bcc = bsc.GetBlobContainerClient(ContainerName);

            // create the container
            bcc.CreateIfNotExists();

            Logger.LogInformation("Storage account accessed");

            DateTime to = DateTime.UtcNow;
            foreach (RepositoryConfiguration repositoryConfiguration in context.RepositoryConfigurations)
            {
                await ExecuteWithRetryAsync(3, async () =>
                {
                    // retrieve the last accessed time for this repository
                    BlobClient bc = bcc.GetBlobClient($"{repositoryConfiguration.Owner}_{repositoryConfiguration.Name}");
                    DateTime lastDateRun = DateTime.UtcNow.AddDays(-1);

                    try
                    {
                        string content = StreamHelpers.GetContentAsString(bc.Download().Value.Content);
                        lastDateRun = DateTime.Parse(content);
                    }
                    catch
                    {
                    }

                    Logger.LogInformation("Last processed date for {0} is {1}", repositoryConfiguration, lastDateRun);

                    string owner = repositoryConfiguration.Owner;
                    string repo = repositoryConfiguration.Name;

                    Logger.LogInformation("Processing repository {0}\\{1}", owner, repo);

                    HtmlPageCreator emailBody = new HtmlPageCreator($"New items in {repo}");

                    // get new issues
                    RetrieveNewItems(context, CreateQueryForNewItems(repositoryConfiguration, IssueIsQualifier.Issue), lastDateRun, emailBody, "New issues");

                    // get new PRs
                    RetrieveNewItems(context, CreateQueryForNewItems(repositoryConfiguration, IssueIsQualifier.PullRequest), lastDateRun, emailBody, "New PRs");

                    // get needs attention issues
                    RetrieveNeedsAttentionIssues(context, repositoryConfiguration, emailBody);

                    emailBody.AddContent($"<p>Last checked range: {lastDateRun} -> {to} </p>");

                    Logger.LogInformation("Sending email...");
                    // send the email
                    EmailSender.SendEmail(context.SendGridToken, context.FromAddress, emailBody.GetContent(), repositoryConfiguration.ToEmail, repositoryConfiguration.CcEmail, $"New issues in the {repo} repo as of {to.ToShortDateString()}", Logger);

                    Logger.LogInformation("Email sent...");

                    bc.Upload(StreamHelpers.GetStreamForString(to.ToUniversalTime().ToString()), overwrite: true);
                    Logger.LogInformation($"Persisted last event time for {repositoryConfiguration.Owner}\\{repositoryConfiguration.Name} as {to}");
                });
            }
        }

        private bool RetrieveNewItems(ReportExecutionContext context, SearchIssuesRequest requestOptions, DateTime from, HtmlPageCreator emailBody, string header)
        {
            TableCreator tc = new TableCreator(header);
            tc.DefineTableColumn("Title", TableCreator.Templates.Title);
            tc.DefineTableColumn("State", i => i.Issue.State.ToString());
            tc.DefineTableColumn("Labels", TableCreator.Templates.Labels);
            tc.DefineTableColumn("Author", TableCreator.Templates.Author);
            tc.DefineTableColumn("Assigned", TableCreator.Templates.Assigned);

            List<ReportIssue> issues = new List<ReportIssue>();
            foreach (Issue issue in context.GitHubClient.SearchForGitHubIssues(requestOptions))
            {
                if (issue.CreatedAt.ToUniversalTime() >= from.ToUniversalTime())
                {
                    issues.Add(new ReportIssue() { Issue = issue, Note = string.Empty, Milestone = null });
                }
            }

            //sort the issues by state, descending becasue Open should show up before Closed
            emailBody.AddContent(tc.GetContent(issues.OrderByDescending(i => i.Issue.State.ToString())));

            return issues.Any();
        }

        private bool RetrieveNeedsAttentionIssues(ReportExecutionContext context, RepositoryConfiguration repositoryConfig, HtmlPageCreator emailBody)
        {
            TableCreator tc = new TableCreator("Issues that need attention");
            tc.DefineTableColumn("Title", TableCreator.Templates.Title);
            tc.DefineTableColumn("Labels", TableCreator.Templates.Labels);
            tc.DefineTableColumn("Author", TableCreator.Templates.Author);
            tc.DefineTableColumn("Assigned", TableCreator.Templates.Assigned);

            List<ReportIssue> issues = new List<ReportIssue>();

            foreach (Issue issue in context.GitHubClient.SearchForGitHubIssues(CreateQueryForNeedsAttentionItems(repositoryConfig)))
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

        private SearchIssuesRequest CreateQueryForNewItems(RepositoryConfiguration repoInfo, IssueIsQualifier issueType)
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

        private SearchIssuesRequest CreateQueryForNeedsAttentionItems(RepositoryConfiguration repoInfo)
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
