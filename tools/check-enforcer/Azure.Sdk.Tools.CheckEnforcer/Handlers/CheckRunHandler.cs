using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Core;

namespace Azure.Sdk.Tools.CheckEnforcer.Handlers
{
    public class CheckRunHandler : Handler<CheckRunEventPayload>
    {
        public CheckRunHandler(IGlobalConfigurationProvider globalConfigurationProvider, IGitHubClientProvider gitHubCLientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, ILogger<CheckRunHandler> logger) : base(globalConfigurationProvider, gitHubCLientProvider, repositoryConfigurationProvider, logger)
        {
        }

        public override string EventName => "check_run";

        protected override IEnumerable<CheckRunEventPayload> FilterPayloads(IEnumerable<CheckRunEventPayload> payloads)
        {
            // TODO: This filtering logic needs to be rationalized with the HandleCoreAsync
            //       method, as it duplicates the filtering we do there.

            // If a user explicitly requests an evaluation via the evaluate button, do it.
            var requestedActionPayloads = from payload in payloads
                                          where payload.CheckRun.Name == this.GlobalConfigurationProvider.GetApplicationName()
                                            && payload.Action == "requested_action"
                                            && payload.RequestedAction.Identifier == "evaluate"
                                          select payload;

            // Here we just want to take completed events since nothing else matters, and
            // only the last one since the evaluation will be the same.
            var elligiblePayloads = from payload in payloads
                                    where payload.CheckRun.Name != this.GlobalConfigurationProvider.GetApplicationName()
                                    where payload.CheckRun.StartedAt < DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(1))
                                    where payload.CheckRun.Status == new StringEnum<CheckStatus>(CheckStatus.Completed)
                                    select payload;

            var everythingElse = from payload in elligiblePayloads
                                 group payload by payload.CheckRun.HeadSha into payloadGroup
                                 select payloadGroup.Last();

            var delta = elligiblePayloads.Count() - everythingElse.Count();

            if (delta > 0)
            {
                Debugger.Break();
            }

            return Enumerable.Concat(requestedActionPayloads, everythingElse);
        }

        protected override async Task HandleCoreAsync(HandlerContext<CheckRunEventPayload> context, CancellationToken cancellationToken)
        {
            var payload = context.Payload;

            var installationId = payload.Installation.Id;
            var repositoryId = payload.Repository.Id;
            var sha = payload.CheckRun.CheckSuite.HeadSha;
            var runIdentifier = $"{installationId}/{repositoryId}/{sha}";

            using (var scope = Logger.BeginScope("Processing check-run event on: {runIdentifier}", runIdentifier))
            {
                if (payload.CheckRun.Name == this.GlobalConfigurationProvider.GetApplicationName() && payload.Action == "requested_action" && payload.RequestedAction.Identifier == "evaluate")
                {
                    Logger.LogInformation(
                        "Responding to check run action button: {identifier}.",
                        payload.RequestedAction.Identifier
                        );

                    await EvaluatePullRequestAsync(context.Client, installationId, repositoryId, sha, cancellationToken);
                }
                else if (payload.CheckRun.Name == this.GlobalConfigurationProvider.GetApplicationName())
                {
                    Logger.LogInformation(
                        "Skipping processing event for: {runIdentifier} because appplication name match.",
                        runIdentifier
                        );
                }
                else if (payload.CheckRun.StartedAt < DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(1)))
                {
                    Logger.LogWarning(
                        "Skipping stake check-run event for: {runIdentifier} because it started {days} ago.",
                        runIdentifier,
                        (DateTimeOffset.UtcNow - payload.CheckRun.StartedAt).Days
                        );
                }
                else
                {
                    try
                    {
                        if (payload.CheckRun.Status != new StringEnum<CheckStatus>(CheckStatus.Completed))
                        {
                            Logger.LogInformation(
                                "Skipping processing event for: {runIdentifier} check-run status not completed.",
                                runIdentifier
                                );
                            return;
                        }

                        var configuration = await this.RepositoryConfigurationProvider.GetRepositoryConfigurationAsync(installationId, repositoryId, sha, cancellationToken);
                        if (configuration.IsEnabled)
                        {
                            Logger.LogInformation(
                                "Check Enforcer was enabled for: {runIdentifier}.",
                                runIdentifier
                                );
                        }
                        else
                        {
                            Logger.LogInformation(
                                "Check Enforcer was disabled for: {runIdentifier}.",
                                runIdentifier
                                );
                            return;
                        }

                        Logger.LogInformation(
                            "Evaluating check-run for: {runIdentifier}.",
                            runIdentifier
                            );

                        await EvaluatePullRequestAsync(context.Client, installationId, repositoryId, sha, cancellationToken);

                        Logger.LogInformation(
                            "Evaluated check-run for: {runIdentifier}.",
                            runIdentifier
                            );
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(
                            ex,
                            "Failed to process check-run event for: {runIdentifier}",
                            runIdentifier
                            );

                        throw ex;
                    }
                }
            }
        }
    }
}
