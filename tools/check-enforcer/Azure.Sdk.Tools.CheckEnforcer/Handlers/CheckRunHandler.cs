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
        private const int EventIdBase = 3000;
        private static readonly EventId AcquiringSemaphoreEventId = new EventId(EventIdBase + 0, "Acquring Semaphore");
        private static readonly EventId AcquiredSemaphoreEventId = new EventId(EventIdBase + 1, "Acquired Semaphore");
        private static readonly EventId ReleasingSemaphoreEventId = new EventId(EventIdBase + 2, "Releasing Semaphore");
        private static readonly EventId ReleasedSemaphoreEventId = new EventId(EventIdBase + 3, "Released Semaphore");
        private static readonly EventId SkippedProcessingTrapSemaphoreEventId = new EventId(EventIdBase + 4, "Skipped Processing (Trap Semaphore)");
        private static readonly EventId WaitingOnTrapSemaphoreEventId = new EventId(EventIdBase + 5, "Waiting on Trap Semaphore");
        private static readonly EventId WaitOnTrapSemaphoreTimeoutEventId = new EventId(EventIdBase + 6, "Wait on Trap Semaphore Timeout");
        private static readonly EventId WaitingOnQueueSemaphoreEventId = new EventId(EventIdBase + 7, "Waiting on Queue Semaphore");
        private static readonly EventId WaitOnQueueSemaphoreTimeoutEventId = new EventId(EventIdBase + 8, "Wait on Queue Semaphore Timeout");
        private static readonly EventId CheckEnforcerEnabledEventId = new EventId(EventIdBase + 9, "Check Enforcer Enabled");
        private static readonly EventId CheckEnforcerDisabledEventId = new EventId(EventIdBase + 10, "Check Enforcer Disabled");
        private static readonly EventId AcquiringDistributedLockEventId = new EventId(EventIdBase + 11, "Acquiring Distributed Lock");
        private static readonly EventId AcquiredDistributedLockEventId = new EventId(EventIdBase + 12, "Acquired Distributed Lock");
        private static readonly EventId ReleasingDistributedLockEventId = new EventId(EventIdBase + 13, "Releasing Distributed Lock");
        private static readonly EventId ReleasedDistributedLockEventId = new EventId(EventIdBase + 14, "Released Distributed Lock");
        private static readonly EventId EvaluatingCheckRunEventId = new EventId(EventIdBase + 15, "Evalating Check Run");
        private static readonly EventId EvaluatedCheckRunEventId = new EventId(EventIdBase + 16, "Evaluated Check Run");
        private static readonly EventId CheckRunEventProcessingFailedEventId = new EventId(EventIdBase + 17, "Check Run Event Processing Failed");
        private static readonly EventId SkippedProcessingCheckEnforcerCheckRunEventEventId = new EventId(EventIdBase + 18, "Skipped Processing Check Enforcer Check Run Event");
        private static readonly EventId SkippedProcessingIncompleteCheckRunEventEventId = new EventId(EventIdBase + 19, "Skipped Processing Incomplete Check Run Event");
        private static readonly EventId FailedToAcquiredDistributedLockEventId = new EventId(EventIdBase + 20, "Failed to acquired distributed lock, giving up.");
        private static readonly EventId SkippedProcessStaleCheckRunEventEventId = new EventId(EventIdBase + 21, "Skipped Porcessing Stale Check Run Event");


        public CheckRunHandler(IGlobalConfigurationProvider globalConfigurationProvider, IGitHubClientProvider gitHubCLientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, ILogger logger) : base(globalConfigurationProvider, gitHubCLientProvider, repositoryConfigurationProvider, logger)
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
                if (payload.CheckRun.Name == this.GlobalConfigurationProvider.GetApplicationName())
                {
                    Logger.LogInformation(
                        SkippedProcessingCheckEnforcerCheckRunEventEventId,
                        "Skipping processing event for: {runIdentifier} because appplication name match.",
                        runIdentifier
                        );
                }
                else if (payload.CheckRun.StartedAt < DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(1)))
                {
                    Logger.LogWarning(
                        SkippedProcessStaleCheckRunEventEventId,
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
                                SkippedProcessingIncompleteCheckRunEventEventId,
                                "Skipping processing event for: {runIdentifier} check-run status not completed.",
                                runIdentifier
                                );
                            return;
                        }

                        var configuration = await this.RepositoryConfigurationProvider.GetRepositoryConfigurationAsync(installationId, repositoryId, sha, cancellationToken);
                        if (configuration.IsEnabled)
                        {
                            Logger.LogInformation(
                                CheckEnforcerEnabledEventId,
                                "Check Enforcer was enabled for: {runIdentifier}.",
                                runIdentifier
                                );
                        }
                        else
                        {
                            Logger.LogInformation(
                                CheckEnforcerDisabledEventId,
                                "Check Enforcer was disabled for: {runIdentifier}.",
                                runIdentifier
                                );
                            return;
                        }

                        Logger.LogInformation(
                            EvaluatingCheckRunEventId,
                            "Evaluating check-run for: {runIdentifier}.",
                            runIdentifier
                            );

                        await EvaluatePullRequestAsync(context.Client, installationId, repositoryId, sha, cancellationToken);

                        Logger.LogInformation(
                            EvaluatedCheckRunEventId,
                            "Evaluated check-run for: {runIdentifier}.",
                            runIdentifier
                            );
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(
                            CheckRunEventProcessingFailedEventId,
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
