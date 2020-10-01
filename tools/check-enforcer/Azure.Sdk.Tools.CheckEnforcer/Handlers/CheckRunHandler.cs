using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer.Handlers
{
    public class CheckRunHandler : Handler<CheckRunEventPayload>
    {
        public CheckRunHandler(IGlobalConfigurationProvider globalConfigurationProvider, IGitHubClientProvider gitHubCLientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, ILogger logger, GitHubRateLimiter limiter) : base(globalConfigurationProvider, gitHubCLientProvider, repositoryConfigurationProvider, logger, limiter)
        {
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
                else if (payload.CheckRun.Name.Contains("("))
                {
                    // HACK: This change short circuits processing of events that come from jobs rather than runs. We
                    //       are leveraging the fact that Azure Pipelines jobs have check names with an opening bracket.
                    //       This is a short term fox to stop the bleeding whilst we figure out a better way to ignore
                    //       notifications from the job level.

                    Logger.LogInformation(
                        "Skipping processing event for: {runIdentifier} because based on the name it is a job, not a run.",
                        runIdentifier
                        );
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
