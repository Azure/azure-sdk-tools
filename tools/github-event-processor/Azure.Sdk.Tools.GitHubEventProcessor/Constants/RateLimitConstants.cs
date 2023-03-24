using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Constants
{
    public class RateLimitConstants
    {
        // https://docs.github.com/en/rest/overview/resources-in-the-rest-api?apiVersion=2022-11-28#rate-limits-for-requests-from-github-actions
        // When using the GITHUB_TOKEN the number of Actions per hour for an Enterprise repository is 15000/hour
        public const int ActionRateLimitEnterprise = 15000;
        // When using the GITHUB_TOKEN the number of Actions per hour for an Non-enterprise repository is 1000/hour
        public const int ActionRateLimitNonEnterprise = 1000;

        // https://docs.github.com/en/rest/search?apiVersion=2022-11-28#about-search
        // The SearchIssues API has a rate limit of 1000 results which resets every 60 seconds
        public const int SearchIssuesRateLimit = 1000;
    }
}
