using ComposableAsync;
using RateLimiter;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub
{
    public static class GitHubRateLimiter
    {
        private static TimeLimiter limiter = TimeLimiter.GetFromMaxCountByInterval(1, TimeSpan.FromSeconds(1));

        public async static Task WaitForGitHubCapacityAsync()
        {
            await limiter;
        }
    }
}
