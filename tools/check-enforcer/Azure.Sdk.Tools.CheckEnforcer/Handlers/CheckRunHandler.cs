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

                if (payload.CheckRun.Status != new StringEnum<CheckStatus>(CheckStatus.Completed))
                {
                    // TODO: Need to evaluate whether this is the best thing to do
                    //       here. Strictly speaking if a check is requequed this means
                    //       that we wouldn't react to it. But this is useful as a way
                    //       of reducing load. You can't get throttled on requeusts you
                    //       don't make!
                    return;
                }

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
