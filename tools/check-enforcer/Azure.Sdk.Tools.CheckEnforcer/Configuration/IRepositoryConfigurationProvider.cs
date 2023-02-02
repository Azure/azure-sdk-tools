using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Configuration
{
    public interface IRepositoryConfigurationProvider
    {
        Task<IRepositoryConfiguration> GetRepositoryConfigurationAsync(long installationId, long repositoryId, string pullRequestSha, CancellationToken cancellationToken);
    }
}
