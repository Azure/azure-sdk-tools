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
    internal class FindIssuesInPastDueMilestones : BaseFunction
    {
        public FindIssuesInPastDueMilestones(ILogger log) : base(log)
        {

        }

        public override void Execute()
        {
            foreach (var repositoryConfig in _cmdLine.RepositoriesList)
            {
                HtmlPageCreator emailBody = new HtmlPageCreator($"Issues in expired milestones for {repositoryConfig.Repo}");

                bool hasFoundIssues = FindIssuesInPastDuesMilestones(repositoryConfig, emailBody);

                if (hasFoundIssues)
                {
                    // send the email
                    EmailSender.SendEmail(_cmdLine.EmailToken, _cmdLine.FromEmail, emailBody.GetContent(), repositoryConfig.ToEmail, repositoryConfig.CcEmail,
                        $"Issues in old milestone for {repositoryConfig.Repo}", _log);
                }
            }
        }

        private bool FindIssuesInPastDuesMilestones(RepositoryConfig repositoryConfig, HtmlPageCreator emailBody)
        {
            TableCreator tc = new TableCreator("Issues in past-due milestones");
            tc.DefineTableColumn("Milestone", TableCreator.Templates.Milestone);
            tc.DefineTableColumn("Title", TableCreator.Templates.Title);
            tc.DefineTableColumn("Labels", TableCreator.Templates.Labels);
            tc.DefineTableColumn("Author", TableCreator.Templates.Author);
            tc.DefineTableColumn("Assigned", TableCreator.Templates.Assigned);
                
            _log.LogInformation($"Retrieving milestone information for repo {repositoryConfig.Repo}");
            IEnumerable<Milestone> milestones = _gitHub.ListMilestones(repositoryConfig).GetAwaiter().GetResult();

            List<Milestone> pastDueMilestones = new List<Milestone>();
            foreach (var item in milestones)
            {
                if (item.DueOn != null && DateTimeOffset.Now > item.DueOn)
                {
                    _log.LogWarning($"Milestone {item.Title} past due ({item.DueOn.Value}) has {item.OpenIssues} open issue(s).");
                    if (item.OpenIssues > 0)
                    {
                        pastDueMilestones.Add(item);
                    }
                }
            }

            _log.LogInformation($"Found {pastDueMilestones.Count} past due milestones with active issues");

            List<ReportIssue> issuesInPastMilestones = new List<ReportIssue>();
            foreach (var item in pastDueMilestones)
            {
                _log.LogInformation($"Retrieve issues for milestone {item.Title}");

                foreach (var issue in _gitHub.SearchForGitHubIssues(CreateQuery(repositoryConfig, item)))
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
