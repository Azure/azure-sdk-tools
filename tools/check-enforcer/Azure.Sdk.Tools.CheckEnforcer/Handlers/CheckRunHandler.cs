using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
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
        public CheckRunHandler(IGlobalConfigurationProvider globalConfigurationProvider, IGitHubClientProvider gitHubCLientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, ILogger logger) : base(globalConfigurationProvider, gitHubCLientProvider, repositoryConfigurationProvider, logger)
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

                await EvaluatePullRequestAsync(
                    context.Client, installationId, repositoryId, sha, cancellationToken
                    );
            }
        }
    }
}
