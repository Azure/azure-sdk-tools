using Azure.Sdk.Tools.GitHubIssues.Services.Configuration;
using Octokit;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GitHubIssues.Helpers
{
    internal static class GitHubHelpers
    {
        public static IEnumerable<Issue> SearchForGitHubIssues(this GitHubClient s_gitHub, SearchIssuesRequest issueQuery)
        {
            List<Issue> totalIssues = new List<Issue>();
            int totalPages = -1, currentPage = 0;

            do
            {
                currentPage++;
                issueQuery.Page = currentPage;

                SearchIssuesResult searchresults = null;

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

            return (await ms.GetAllForRepository(repo.Owner, repo.Name)).ToList();
        }
    }
}
