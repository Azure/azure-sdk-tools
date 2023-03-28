using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Constants
{
    public class RateLimitConstants
    {
        // https://docs.github.com/en/rest/search?apiVersion=2022-11-28#about-search
        // The SearchIssues API has a rate limit of 1000 results which resets every 60 seconds
        public const int SearchIssuesRateLimit = 1000;
    }
}
