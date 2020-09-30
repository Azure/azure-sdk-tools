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
    public class FindIssuesInBacklogMilestones : BaseReport
    {
        public FindIssuesInBacklogMilestones(IConfigurationService configurationService, ILogger<FindIssuesInBacklogMilestones> logger) : base(configurationService, logger)
        {
        }

        protected override async Task ExecuteCoreAsync(ReportExecutionContext context)
        {
            foreach (RepositoryConfiguration repositoryConfiguration in context.RepositoryConfigurations)
            {
                await ExecuteWithRetryAsync(3, async () => {
                    HtmlPageCreator emailBody = new HtmlPageCreator($"Issues in expired milestones for {repositoryConfiguration.Name}");
                    bool hasFoundIssues = GetIssuesInBacklogMilestones(context, repositoryConfiguration, emailBody);

                    if (hasFoundIssues)
                    {
                        // send the email
                        EmailSender.SendEmail(context.SendGridToken, context.FromAddress, emailBody.GetContent(), repositoryConfiguration.ToEmail, repositoryConfiguration.CcEmail,
                            $"Issues in old milestone for {repositoryConfiguration.Name}", Logger);
                    }
                });
            }
        }

        private bool GetIssuesInBacklogMilestones(ReportExecutionContext context, RepositoryConfiguration repositoryConfig, HtmlPageCreator emailBody)
        {
            Logger.LogInformation($"Retrieving milestone information for repo {repositoryConfig.Name}");
            IEnumerable<Milestone> milestones = context.GitHubClient.ListMilestones(repositoryConfig).GetAwaiter().GetResult();

            List<Milestone> backlogMilestones = new List<Milestone>();
            foreach (Milestone item in milestones)
            {
                if (item.DueOn == null)
                {
                    Logger.LogWarning($"Milestone {item.Title} has {item.OpenIssues} open issue(s).");
                    if (item.OpenIssues > 0)
                    {
                        backlogMilestones.Add(item);
                    }
                }
            }

            Logger.LogInformation($"Found {backlogMilestones.Count} past due milestones with active issues");
            List<ReportIssue> issuesInBacklogMilestones = new List<ReportIssue>();
            foreach (Milestone item in backlogMilestones)
            {
                Logger.LogInformation($"Retrieve issues for milestone {item.Title}");

                foreach (Issue issue in context.GitHubClient.SearchForGitHubIssues(CreateQuery(repositoryConfig, item)))
                {
                    issuesInBacklogMilestones.Add(new ReportIssue()
                    {
                        Issue = issue,
                        Milestone = item,
                        Note = string.Empty
                    });
                }
            }

            // Split the list into 3:
            // > 12months
            // > 6months
            // 0-6months

            IEnumerable<IGrouping<string, ReportIssue>> groups = issuesInBacklogMilestones.GroupBy(i =>
                                                    i.Issue.CreatedAt > DateTime.Now.AddMonths(-6) ?
                                                        "C. Issues created in the last 6 months" :
                                                        i.Issue.CreatedAt <= DateTime.Now.AddMonths(-6) && i.Issue.CreatedAt > DateTime.Now.AddMonths(-12) ?
                                                            "B. Issues created between 6 and 12 months ago" :
                                                            "A. Issues created more than 12 months ago");

            foreach (IGrouping<string, ReportIssue> group in groups.OrderBy(g => g.Key))
            {
                TableCreator tc = new TableCreator(group.Key);
                tc.DefineTableColumn("Milestone", TableCreator.Templates.Milestone);
                tc.DefineTableColumn("Created", i => i.Issue.CreatedAt.UtcDateTime.ToShortDateString());
                tc.DefineTableColumn("Title", TableCreator.Templates.Title);
                tc.DefineTableColumn("Labels", TableCreator.Templates.Labels);
                tc.DefineTableColumn("Author", TableCreator.Templates.Author);
                tc.DefineTableColumn("Assigned", TableCreator.Templates.Assigned);

                emailBody.AddContent(tc.GetContent(group));
            }

            return issuesInBacklogMilestones.Any();
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
