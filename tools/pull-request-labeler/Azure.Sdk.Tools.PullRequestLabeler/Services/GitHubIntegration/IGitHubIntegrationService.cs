using Octokit;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.PullRequestLabeler.Services.GitHubIntegration
{
    public interface IGitHubIntegrationService
    {
        Task<GitHubClient> GetGitHubInstallationClientAsync(int installationId);
    }
}
