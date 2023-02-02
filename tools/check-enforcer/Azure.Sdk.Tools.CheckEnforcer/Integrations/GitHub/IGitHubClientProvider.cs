using Octokit;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub
{
    public interface IGitHubClientProvider
    {
        Task<GitHubClient> GetApplicationClientAsync(CancellationToken cancellationToken);
        Task<GitHubClient> GetInstallationClientAsync(long installationId, CancellationToken cancellationToken);
    }
}
