using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Octokit;

namespace Azure.Sdk.Tools.GithubEventProcessor.Utils
{
    public class RateLimitUtil
    {
        public static async Task writeRateLimits(GitHubClient gitHubClient, string prependMessage = null)
        {
            var miscRateLimit = await gitHubClient.RateLimit.GetRateLimits();
            string rateLimitMessage = $"Limit={miscRateLimit.Resources.Core.Limit}, Remaining={miscRateLimit.Resources.Core.Remaining}";
            if (prependMessage != null)
            {
                rateLimitMessage = $"{prependMessage} {rateLimitMessage}";
            }
            Console.WriteLine(rateLimitMessage);
        }
    }
}
