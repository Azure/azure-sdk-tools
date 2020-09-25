using Azure.Sdk.Tools.GitHubIssues.Services.Configuration;
using ComposableAsync;
using Octokit;
using RateLimiter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GitHubIssues.Helpers
{
    internal static class GitHubHelpers
    {
        private static TimeLimiter rateLimiter = TimeLimiter.GetFromMaxCountByInterval(1, TimeSpan.FromSeconds(1));

        private static void WaitForGitHubCapacity()
        {
            rateLimiter.GetAwaiter().GetResult();
        }

        public static IEnumerable<Issue> SearchForGitHubIssues(this GitHubClient s_gitHub, SearchIssuesRequest issueQuery)
        {
            List<Issue> totalIssues = new List<Issue>();
            int totalPages = -1, currentPage = 0;

            do
            {
                currentPage++;
                issueQuery.Page = currentPage;

                SearchIssuesResult searchresults = null;

                WaitForGitHubCapacity();
                searchresults = s_gitHub.Search.SearchIssues(issueQuery).Result;

                foreach (Issue item in searchresults.Items)
                {
                    totalIssues.Add(item);
                }

                // if this is the first call, setup the totalpages stuff
                if (totalPages == -1)
                {
                    totalPages = (searchresults.TotalCount / 100) + 1;
                }
            } while (totalPages > currentPage);

            return totalIssues;
        }

        public static async Task<IEnumerable<Milestone>> ListMilestones(this GitHubClient s_gitHub, RepositoryConfiguration repo)
        {
            MilestonesClient ms = new MilestonesClient(new ApiConnection(s_gitHub.Connection));

            WaitForGitHubCapacity();
            return (await ms.GetAllForRepository(repo.Owner, repo.Name)).ToList();
        }
    }
}
