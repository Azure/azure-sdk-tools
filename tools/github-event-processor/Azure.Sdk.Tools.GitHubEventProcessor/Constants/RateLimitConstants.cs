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
        // https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api?apiVersion=2022-11-28#about-primary-rate-limits
        // There's a 500/hour limit on content creation. In theory, Closing an issue, Locking an issue and
        // creating a comment are all considered content creation.
        public const int ContentCreationRateLimit = 300;
        // The actual rate limit per minute for content creation is 80 but to ensure that scheduled tasks
        // don't interfere with Actions processing or people doing things in the GitHub UI.
        public const int ScheduledUpdatesPerMinuteRateLimit = 50;
    }
}
