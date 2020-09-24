using GitHubIssues;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.GitHubIssues.Services.Configuration
{
    public interface IConfigurationService
    {
        Task<IEnumerable<RepositoryConfiguration>> GetRepositoryConfigurationsAsync();
        Task<string> GetGitHubPersonalAccessTokenAsync();
        Task<string> GetFromAddressAsync();
        Task<string> GetSendGridTokenAsync();
        Task<string> GetApplicationIDAsync();
        Task<string> GetApplicationNameAsync();
        Task<int> GetMaxRequestsPerPeriodAsync();
        Task<int> GetPeriodDurationInSecondsAsync();
    }
}
