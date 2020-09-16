using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using ComposableAsync;
using RateLimiter;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub
{
    public class GitHubRateLimiter
    {
        public GitHubRateLimiter(IGlobalConfigurationProvider globalConfigurationProvider)
        {
            limiter = TimeLimiter.GetFromMaxCountByInterval(
                globalConfigurationProvider.GetMaxRequestsPerPeriod(),
                TimeSpan.FromSeconds(globalConfigurationProvider.GetPeriodDurationInSeconds())
                );
        }

        private TimeLimiter limiter;

        public async Task WaitForGitHubCapacityAsync()
        {
            await limiter;
        }
    }
}
