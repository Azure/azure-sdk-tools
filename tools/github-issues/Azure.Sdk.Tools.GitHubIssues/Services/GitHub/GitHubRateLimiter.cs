using Azure.Sdk.Tools.GitHubIssues.Configuration;
using ComposableAsync;
using RateLimiter;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubIssues.Integrations.GitHub
{
    public class GitHubRateLimiter
    {
        public GitHubRateLimiter(IConfigurationService configurationService)
        {
            limiter = TimeLimiter.GetFromMaxCountByInterval(
                configurationService.GetMaxRequestsPerPeriod(),
                TimeSpan.FromSeconds(configurationService.GetPeriodDurationInSeconds())
                );
        }

        private TimeLimiter limiter;

        public async Task WaitForGitHubCapacityAsync()
        {
            await limiter;
        }
    }
}
