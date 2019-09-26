using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Handlers
{
    public class CheckSuiteHandler : Handler<CheckSuiteEventPayload>
    {
        public CheckSuiteHandler(IGlobalConfigurationProvider globalConfigurationProvider, IGitHubClientProvider gitHubClientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, ILogger logger) : base(globalConfigurationProvider, gitHubClientProvider, repositoryConfigurationProvider, logger)
        {
        }

        protected override async Task HandleCoreAsync(HandlerContext<CheckSuiteEventPayload> context, CancellationToken cancellationToken)
        {
            var payload = context.Payload;

            if (payload.Action == "requested" || payload.Action == "rerequested")
            {
                var installationId = payload.Installation.Id;
                var repositoryId = payload.Repository.Id;
                var sha = payload.CheckSuite.HeadSha;

                var configuration = await this.RepositoryConfigurationProvider.GetRepositoryConfigurationAsync(installationId, repositoryId, sha, cancellationToken);

                if (configuration.IsEnabled)
                {
                    await CreateCheckAsync(context.Client, repositoryId, sha, true, cancellationToken);
                }
            }
        }
    }
}
