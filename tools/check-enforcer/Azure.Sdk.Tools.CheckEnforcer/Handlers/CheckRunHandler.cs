using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Sdk.Tools.CheckEnforcer.Locking;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Handlers
{
    public class CheckRunHandler : Handler<CheckRunEventPayload>
    {
        public CheckRunHandler(IGlobalConfigurationProvider globalConfigurationProvider, IGitHubClientProvider gitHubCLientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, IDistributedLockProvider distributedLockProvider, ILogger logger) : base(globalConfigurationProvider, gitHubCLientProvider, repositoryConfigurationProvider, distributedLockProvider, logger)
        {
        }


        protected override async Task HandleCoreAsync(HandlerContext<CheckRunEventPayload> context, CancellationToken cancellationToken)
        {
            var payload = context.Payload;

            if (payload.CheckRun.Name != this.GlobalConfigurationProvider.GetApplicationName())
            {
                // Extract critical info for payload.
                var installationId = payload.Installation.Id;
                var repositoryId = payload.Repository.Id;
                var sha = payload.CheckRun.CheckSuite.HeadSha;

                var distributedLockIdentifier = $"{installationId}/{repositoryId}/{sha}";

                var configuration = await this.RepositoryConfigurationProvider.GetRepositoryConfigurationAsync(installationId, repositoryId, sha, cancellationToken);

                if (configuration.IsEnabled)
                {
                    using (var distributedLock = DistributedLockProvider.Create(distributedLockIdentifier))
                    {
                        await distributedLock.AcquireAsync();

                        await EvaluatePullRequestAsync(
                            context.Client, installationId, repositoryId, sha, cancellationToken
                            );

                        await distributedLock.ReleaseAsync();
                    }
                }

            }
        }
    }
}
