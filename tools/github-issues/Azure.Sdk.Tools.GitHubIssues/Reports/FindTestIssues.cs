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
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubIssues.Reports
{
    public class FindTestIssues : BaseReport
    {
        public FindTestIssues(IConfigurationService configurationService, ILogger<FindIssuesInPastDueMilestones> logger) : base(configurationService, logger)
        {
        }

        private string[] _labels = new string[] { "test-reliability", "test-manual-pass", "test-sovereign-cloud" };

        protected override async Task ExecuteCoreAsync(ReportExecutionContext context)
        {
            foreach (RepositoryConfiguration repositoryConfiguration in context.RepositoryConfigurations)
            {
                await ExecuteWithRetryAsync(3, async () =>
                {
                    HtmlPageCreator emailBody = new HtmlPageCreator($"Test related issues in {repositoryConfiguration.Name}");
                    bool hasFoundIssues = FindIssuesWithLabel(context, repositoryConfiguration, emailBody);

                    if (hasFoundIssues)
                    {
                        // send the email
                        EmailSender.SendEmail(context.SendGridToken, context.FromAddress, emailBody.GetContent(), repositoryConfiguration.ToEmail, repositoryConfiguration.CcEmail,
                            $"Issues with test-* labels for {repositoryConfiguration.Name}", Logger);
                    }
                });
            }
        }

        private bool FindIssuesWithLabel(ReportExecutionContext context, RepositoryConfiguration repositoryConfig, HtmlPageCreator emailBody)
        {
            List<ReportIssue> testIssues = new List<ReportIssue>();
            foreach (string label in _labels)
            {
                Logger.LogInformation($"Retrieve issues for label {label}");

                foreach (Issue issue in context.GitHubClient.SearchForGitHubIssues(CreateQuery(repositoryConfig, label)))
                {
                    testIssues.Add(new ReportIssue()
                    {
                        Issue = issue,
                        Note = $"Label: {label}"
                    });
                }
            }

            IEnumerable<IGrouping<string, ReportIssue>> groups = testIssues.GroupBy(g=>g.Note);

            foreach (IGrouping<string, ReportIssue> group in groups.OrderBy(g => g.Key))
            {
                TableCreator tc = new TableCreator(group.Key);
                tc.DefineTableColumn("Milestone", TableCreator.Templates.Milestone);
                tc.DefineTableColumn("Title", TableCreator.Templates.Title);
                tc.DefineTableColumn("Labels", TableCreator.Templates.Labels);
                tc.DefineTableColumn("Author", TableCreator.Templates.Author);
                tc.DefineTableColumn("Assigned", TableCreator.Templates.Assigned);

                emailBody.AddContent(tc.GetContent(group));
            }

            return testIssues.Any();
        }
        private SearchIssuesRequest CreateQuery(RepositoryConfiguration repoInfo, string label)
        {
            // Find all open issues
            //  That are marked with 'label'

            SearchIssuesRequest requestOptions = new SearchIssuesRequest()
            {
                Repos = new RepositoryCollection(),
                Labels = new string[] { label },
                State = ItemState.Open
            };

            requestOptions.Repos.Add(repoInfo.Owner, repoInfo.Name);

            return requestOptions;
        }
    }
}
