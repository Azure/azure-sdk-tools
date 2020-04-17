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
    internal class FindIssuesInBacklogMilestones : BaseFunction
    {
        public FindIssuesInBacklogMilestones(ILogger log) : base(log)
        {

        }

        public override void Execute()
        {
            foreach (var repositoryConfig in _cmdLine.RepositoriesList)
            {
                HtmlPageCreator emailBody = new HtmlPageCreator($"Issues in expired milestones for {repositoryConfig.Repo}");
                bool hasFoundIssues = GetIssuesInBacklogMilestones(repositoryConfig, emailBody);

                if (hasFoundIssues)
                {
                    // send the email
                    EmailSender.SendEmail(_cmdLine.EmailToken, _cmdLine.FromEmail, emailBody.GetContent(), repositoryConfig.ToEmail, repositoryConfig.CcEmail,
                        $"Issues in old milestone for {repositoryConfig.Repo}", _log);
                }
            }
        }

        private bool GetIssuesInBacklogMilestones(RepositoryConfig repositoryConfig, HtmlPageCreator emailBody)
        {
            _log.LogInformation($"Retrieving milestone information for repo {repositoryConfig.Repo}");
            IEnumerable<Milestone> milestones = _gitHub.ListMilestones(repositoryConfig).GetAwaiter().GetResult();

            List<Milestone> backlogMilestones = new List<Milestone>();
            foreach (var item in milestones)
            {
                if (item.DueOn == null)
                {
                    _log.LogWarning($"Milestone {item.Title} has {item.OpenIssues} open issue(s).");
                    if (item.OpenIssues > 0)
                    {
                        backlogMilestones.Add(item);
                    }
                }
            }

            _log.LogInformation($"Found {backlogMilestones.Count} past due milestones with active issues");
            List<ReportIssue> issuesInBacklogMilestones = new List<ReportIssue>();
            foreach (var item in backlogMilestones)
            {
                _log.LogInformation($"Retrieve issues for milestone {item.Title}");

                foreach (var issue in _gitHub.SearchForGitHubIssues(CreateQuery(repositoryConfig, item)))
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

            var groups = issuesInBacklogMilestones.GroupBy(i =>
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

        private SearchIssuesRequest CreateQuery(RepositoryConfig repoInfo, Milestone milestone)
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

            requestOptions.Repos.Add(repoInfo.Owner, repoInfo.Repo);

            return requestOptions;
        }
    }
}
