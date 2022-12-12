using System;
using System.Collections.Generic;
using System.Text;
using Octokit;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GithubEventProcessor.Utils
{
    public class SearchUtil
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="gitHubClient"></param>
        /// <param name="repoOwner"></param>
        /// <param name="repoName"></param>
        /// <param name="issueType">IssueTypeQualifier of Issue or PullRequest</param>
        /// <param name="itemState">ItemState of Open or Closed</param>
        /// <param name="issueIsQualifiers">Optional: List of IssueIsQualifier (ex. locked/unlocked) to include, null if none</param>
        /// <param name="labelsToInclude">Optional: List of labels to include, null if none</param>
        /// <param name="labelsToExclude">Optional: List of labels to exclude, null if none</param>
        /// <param name="daysSinceLastUpdate">Optional: Number of days since last updated </param>
        /// <returns></returns>
        public static async Task<SearchIssuesResult> QueryIssues(GitHubClient gitHubClient, 
            string repoOwner, 
            string repoName,
            IssueTypeQualifier issueType,
            ItemState itemState,
            int daysSinceLastUpdate = 0,
            List<IssueIsQualifier> issueIsQualifiers= null,
            List<string> labelsToInclude = null,
            List<string> labelsToExclude = null)
        {
            var request = new SearchIssuesRequest();

            // Hopefully we're able to get the owner/repo from whatever github response is sent
            // to the cron job
            request.Repos.Add(repoOwner, repoName);

            // Can only search for opened or closed
            request.State = itemState;
            if (null != issueIsQualifiers)
            {
                request.Is = issueIsQualifiers;
            }

            // restrict the search to issues (IssueTypeQualifier.Issue)
            // or pull requests (IssueTypeQualifier.PullRequest)
            request.Type = issueType;

            if (daysSinceLastUpdate > 0)
            {
                // Octokit's DateRange wants a DateTimeOffset as other constructors are depricated
                DateTime daysAgo = DateTime.Now.Subtract(TimeSpan.FromDays(daysSinceLastUpdate));
                DateTimeOffset daysAgoOffset = new DateTimeOffset(daysAgo);
                request.Updated = new DateRange(daysAgoOffset, SearchQualifierOperator.LessThan);
            }

            if (null != labelsToInclude)
            {
                request.Labels = labelsToInclude;
            }

            if (null != labelsToExclude)
            {
                // This is how things would get exluded. Anything that needs to be an exclusion
                // for the query needs added to a SearchIssuesRequestExclusions and then
                // the Exclusions on the request needs to be set to that.
                var exclusions = new SearchIssuesRequestExclusions();
                exclusions.Labels = labelsToExclude;
                request.Exclusions = exclusions;
            }
            var searchIssueResult = await gitHubClient.Search.SearchIssues(request);
            return searchIssueResult;
        }
        public static async Task<SearchIssuesResult> SearchIssuesTest(GitHubClient gitHubClient)
        {
            var request = new SearchIssuesRequest();

            // Hopefully we're able to get the owner/repo from whatever github response is sent
            // to the cron job
            request.Repos.Add("JimSuplizio", "azure-sdk-tools");

            // Can only search for opened or closed
            request.State = ItemState.Open;

            // restrict the search to issues (IssueTypeQualifier.Issue)
            // or pull requests (IssueTypeQualifier.PullRequest)
            request.Type = IssueTypeQualifier.Issue;

            // This is how to filter to get things that have not been updated in more than
            // X days.
            DateTime aWeekAgo = DateTime.Now.Subtract(TimeSpan.FromDays(7));
            DateTimeOffset aWeekAgoOffset = new DateTimeOffset(aWeekAgo);
            // Or the DateTimeOffset.Now.Subtract(TimeSpan.FromDays(7)) could be passed into
            // the DateRange constructor as the first argument
            // request.Updated = new DateRange(DateTimeOffset.Now.Subtract(TimeSpan.FromDays(7)), SearchQualifierOperator.GreaterThan);
            request.Updated = new DateRange(aWeekAgoOffset, SearchQualifierOperator.LessThan);

            // The labels on the request are "contains label"
            List<string> searchForlabels = new List<string>
            {
                "bug"
            };
            request.Labels = searchForlabels;

            // This is how things would get exluded. Anything that needs to be an exclusion
            // for the query needs added to a SearchIssuesRequestExclusions and then
            // the Exclusions on the request needs to be set to that.
            var exclusions = new SearchIssuesRequestExclusions();
            List<string> excludeLabels = new List<string>
            {
                "documentation"
            };
            exclusions.Labels = excludeLabels;
            request.Exclusions = exclusions;

            var searchIssueResult = await gitHubClient.Search.SearchIssues(request);
            return searchIssueResult;
        }
    }
}
