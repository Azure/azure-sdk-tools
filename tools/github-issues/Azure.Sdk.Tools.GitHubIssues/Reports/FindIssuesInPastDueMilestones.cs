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
    public class FindIssuesInPastDueMilestones : BaseReport
    {
        public FindIssuesInPastDueMilestones(IConfigurationService configurationService, ILogger<FindIssuesInPastDueMilestones> logger) : base(configurationService, logger)
        {

        }

        protected override async Task ExecuteCoreAsync(ReportExecutionContext context)
        {
            foreach (RepositoryConfiguration repositoryConfiguration in context.RepositoryConfigurations)
            {
                await ExecuteWithRetryAsync(3, async () =>
                {
                    HtmlPageCreator emailBody = new HtmlPageCreator($"Issues in expired milestones for {repositoryConfiguration.Name}");

                    bool hasFoundIssues = FindIssuesInPastDuesMilestones(context, repositoryConfiguration, emailBody);

                    if (hasFoundIssues)
                    {
                        // send the email
                        EmailSender.SendEmail(context.SendGridToken, context.FromAddress, emailBody.GetContent(), repositoryConfiguration.ToEmail, repositoryConfiguration.CcEmail,
                            $"Issues in old milestone for {repositoryConfiguration.Name}", Logger);
                    }
                });
            }
        }

        private bool FindIssuesInPastDuesMilestones(ReportExecutionContext context, RepositoryConfiguration repositoryConfig, HtmlPageCreator emailBody)
        {
            TableCreator tc = new TableCreator("Issues in past-due milestones");
            tc.DefineTableColumn("Milestone", TableCreator.Templates.Milestone);
            tc.DefineTableColumn("Title", TableCreator.Templates.Title);
            tc.DefineTableColumn("Labels", TableCreator.Templates.Labels);
            tc.DefineTableColumn("Author", TableCreator.Templates.Author);
            tc.DefineTableColumn("Assigned", TableCreator.Templates.Assigned);

            Logger.LogInformation($"Retrieving milestone information for repo {repositoryConfig.Name}");
            IEnumerable<Milestone> milestones = context.GitHubClient.ListMilestones(repositoryConfig).GetAwaiter().GetResult();

            List<Milestone> pastDueMilestones = new List<Milestone>();
            foreach (Milestone item in milestones)
            {
                if (item.DueOn != null && DateTimeOffset.Now > item.DueOn)
                {
                    Logger.LogWarning($"Milestone {item.Title} past due ({item.DueOn.Value}) has {item.OpenIssues} open issue(s).");
                    if (item.OpenIssues > 0)
                    {
                        pastDueMilestones.Add(item);
                    }
                }
            }

            Logger.LogInformation($"Found {pastDueMilestones.Count} past due milestones with active issues");

            List<ReportIssue> issuesInPastMilestones = new List<ReportIssue>();
            foreach (Milestone item in pastDueMilestones)
            {
                Logger.LogInformation($"Retrieve issues for milestone {item.Title}");

                foreach (Issue issue in context.GitHubClient.SearchForGitHubIssues(CreateQuery(repositoryConfig, item)))
                {
                    issuesInPastMilestones.Add(new ReportIssue()
                    {
                        Issue = issue,
                        Milestone = item,
                        Note = string.Empty
                    });
                }
            }

            emailBody.AddContent(tc.GetContent(issuesInPastMilestones));
            return issuesInPastMilestones.Any();
        }
        private SearchIssuesRequest CreateQuery(RepositoryConfiguration repoInfo, Milestone milestone)
        {
            // Find all open issues
            //  That are marked with 'Client
            //  In the specified milestone

            SearchIssuesRequest requestOptions = new SearchIssuesRequest()
            {
                Repos = new RepositoryCollection(),
                Labels = new string[] { },
                State = ItemState.Open,
                Milestone = milestone.Title
            };

            requestOptions.Repos.Add(repoInfo.Owner, repoInfo.Name);

            return requestOptions;
        }
    }
}
