using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Sdk.Tools.CheckEnforcer.Locking;
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
        private static readonly EventId AcquiringSemaphoreEventId = new EventId(0, "Acquring Semaphore");
        private static readonly EventId AcquiredSemaphoreEventId = new EventId(1, "Acquired Semaphore");
        private static readonly EventId ReleasingSemaphoreEventId = new EventId(2, "Releasing Semaphore");
        private static readonly EventId ReleasedSemaphoreEventId = new EventId(3, "Released Semaphore");
        private static readonly EventId SkippedProcessingTrapSemaphoreEventId = new EventId(4, "Skipped Processing (Trap Semaphore)");
        private static readonly EventId WaitingOnTrapSemaphoreEventId = new EventId(5, "Waiting on Trap Semaphore");
        private static readonly EventId WaitOnTrapSemaphoreTimeoutEventId = new EventId(6, "Wait on Trap Semaphore Timeout");
        private static readonly EventId WaitingOnQueueSemaphoreEventId = new EventId(7, "Waiting on Queue Semaphore");
        private static readonly EventId WaitOnQueueSemaphoreTimeoutEventId = new EventId(8, "Wait on Queue Semaphore Timeout");
        private static readonly EventId CheckEnforcerEnabledEventId = new EventId(9, "Check Enforcer Enabled");
        private static readonly EventId CheckEnforcerDisabledEventId = new EventId(10, "Check Enforcer Disabled");
        private static readonly EventId AcquiringDistributedLockEventId = new EventId(11, "Acquiring Distributed Lock");
        private static readonly EventId AcquiredDistributedLockEventId = new EventId(12, "Acquired Distributed Lock");
        private static readonly EventId ReleasingDistributedLockEventId = new EventId(13, "Releasing Distributed Lock");
        private static readonly EventId ReleasedDistributedLockEventId = new EventId(14, "Released Distributed Lock");
        private static readonly EventId EvaluatingCheckRunEventId = new EventId(15, "Evalating Check Run");
        private static readonly EventId EvaluatedCheckRunEventId = new EventId(16, "Evaluated Check Run");
        private static readonly EventId CheckRunEventProcessingFailedEventId = new EventId(17, "Check Run Event Processing Failed");
        private static readonly EventId SkippedProcessingCheckEnforcerCheckRunEventEventId = new EventId(18, "Skipped Processing Check Enforcer Check Run Event");
        private static readonly EventId SkippedProcessingIncompleteCheckRunEventEventId = new EventId(19, "Skipped Processing Incomplete Check Run Event");
        private static readonly EventId FailedToAcquiredDistributedLockEventId = new EventId(20, "Failed to acquired distributed lock, giving up.");

        public CheckRunHandler(IGlobalConfigurationProvider globalConfigurationProvider, IGitHubClientProvider gitHubCLientProvider, IRepositoryConfigurationProvider repositoryConfigurationProvider, IDistributedLockProvider distributedLockProvider, ILogger logger) : base(globalConfigurationProvider, gitHubCLientProvider, repositoryConfigurationProvider, distributedLockProvider, logger)
        {
        }

        private static ConcurrentDictionary<string, SemaphoreSlim> semaphores = new ConcurrentDictionary<string, SemaphoreSlim>();

        private SemaphoreSlim GetSemaphore(string semaphoreName)
        {
            Logger.LogDebug(AcquiringSemaphoreEventId, "Acquiring semaphore: {semaphoreName}.", semaphoreName);
            var semaphore = semaphores.GetOrAdd(semaphoreName, new SemaphoreSlim(1, 1));
            var currentCount = semaphore.CurrentCount;
            Logger.LogDebug(
                AcquiredSemaphoreEventId,
                "Acquired semaphore: {semaphoreName} with current count of: {currentCount}.",
                semaphoreName,
                currentCount
                );
            return semaphore;
        }

        protected override async Task HandleCoreAsync(HandlerContext<CheckRunEventPayload> context, CancellationToken cancellationToken)
        {
            var payload = context.Payload;

            var installationId = payload.Installation.Id;
            var repositoryId = payload.Repository.Id;
            var sha = payload.CheckRun.CheckSuite.HeadSha;
            var distributedLockIdentifier = $"{installationId}/{repositoryId}/{sha}";

            using (var scope = Logger.BeginScope("Processing check-run event on: {distributedLockIdentifier}", distributedLockIdentifier))
            {
                if (payload.CheckRun.Name == this.GlobalConfigurationProvider.GetApplicationName())
                {
                    Logger.LogDebug(
                        SkippedProcessingCheckEnforcerCheckRunEventEventId,
                        "Skipping processing event for: {distributedLockIdentifier}",
                        distributedLockIdentifier
                        );
                }
                else
                {
                    try
                    {
                        if (payload.CheckRun.Status != new StringEnum<CheckStatus>(CheckStatus.Completed))
                        {
                            Logger.LogDebug(
                                SkippedProcessingIncompleteCheckRunEventEventId,
                                "Skipping processing event for: {distributedLockIdentifier}",
                                distributedLockIdentifier
                                );
                            return;
                        }

                        var trapSemaphore = GetSemaphore($"trap/{distributedLockIdentifier}");
                        var queueSemaphore = GetSemaphore($"queue/{distributedLockIdentifier}");

                        if (trapSemaphore.CurrentCount == 0)
                        {
                            Logger.LogDebug(
                                SkippedProcessingTrapSemaphoreEventId,
                                "Skipped processing check-run event: {distributedLockIdentifier} because semaphore current count was zero."
                                );
                            return;
                        }

                        Logger.LogDebug(
                            WaitingOnTrapSemaphoreEventId,
                            "Waiting on trap semaphore for: {distributedLockIdentifier}",
                            distributedLockIdentifier
                            );

                        var trapWaitSuccessful = await trapSemaphore.WaitAsync(10000, cancellationToken);

                        if (!trapWaitSuccessful)
                        {
                            Logger.LogWarning(
                                WaitOnTrapSemaphoreTimeoutEventId,
                                "Timed out waiting in trap semaphore for: {distributedLockIdentifier}.",
                                distributedLockIdentifier
                                );
                            return;
                        }

                        Logger.LogDebug(
                            WaitingOnQueueSemaphoreEventId,
                            "Waiting on queue semaphore for: {distributedLockIdentifier}",
                            distributedLockIdentifier
                            );

                        var queueWaitSuccessful = await queueSemaphore.WaitAsync(10000, cancellationToken);

                        if (!queueWaitSuccessful)
                        {
                            Logger.LogWarning(
                                WaitOnQueueSemaphoreTimeoutEventId,
                                "Timed out waiting in queue semaphore for: {distributedLockIdentifier}.",
                                distributedLockIdentifier
                                );
                            return;
                        }

                        var configuration = await this.RepositoryConfigurationProvider.GetRepositoryConfigurationAsync(installationId, repositoryId, sha, cancellationToken);
                        if (configuration.IsEnabled)
                        {
                            Logger.LogInformation(
                                CheckEnforcerEnabledEventId,
                                "Check Enforcer was enabled for: {distributedLockIdentifier}.",
                                distributedLockIdentifier
                                );
                        }
                        else
                        {
                            Logger.LogInformation(
                                CheckEnforcerDisabledEventId,
                                "Check Enforcer was disabled for: {distributedLockIdentifier}.",
                                distributedLockIdentifier
                                );
                            return;
                        }

                        Logger.LogDebug(
                            AcquiringDistributedLockEventId,
                            "Acquiring distributed lock for: {distributedLockIdentifier}.",
                            distributedLockIdentifier
                            );

                        var distributedLock = this.DistributedLockProvider.Create(distributedLockIdentifier);
                        var distributedLockAquired = await distributedLock.AcquireAsync();

                        if (!distributedLockAquired)
                        {
                            Logger.LogWarning(
                                FailedToAcquiredDistributedLockEventId,
                                "Failed to acquire distributed lock for: {distributedLockIdentifier}.",
                                distributedLockIdentifier
                                );

                            // Quickly clean up and get out of here.
                            trapSemaphore.Release();
                            queueSemaphore.Release();
                            return;
                        }

                        Logger.LogDebug(
                            ReleasingSemaphoreEventId,
                            "Releasing trap semaphore for: {distributedLockIdentifier}.",
                            distributedLockIdentifier
                            );

                        trapSemaphore.Release();

                        Logger.LogDebug(
                            ReleasedSemaphoreEventId,
                            "Released trap semaphore for: {distributedLockIdentifier}.",
                            distributedLockIdentifier
                            );

                        Logger.LogInformation(
                            EvaluatingCheckRunEventId,
                            "Evaluating check-run for: {distributedLockIdentifier}.",
                            distributedLockIdentifier
                            );

                        await EvaluatePullRequestAsync(context.Client, installationId, repositoryId, sha, cancellationToken);

                        Logger.LogInformation(
                            EvaluatedCheckRunEventId,
                            "Evaluated check-run for: {distributedLockIdentifier}.",
                            distributedLockIdentifier
                            );

                        Logger.LogDebug(
                            ReleasingDistributedLockEventId,
                            "Releasing distributed lock for: {distributedLockIdentifier}.",
                            distributedLockIdentifier
                            );

                        await distributedLock.ReleaseAsync();

                        Logger.LogDebug(
                            ReleasedDistributedLockEventId,
                            "Released distributed lock for: {distributedLockIdentifier}.",
                            distributedLockIdentifier
                            );

                        Logger.LogDebug(
                            ReleasingSemaphoreEventId,
                            "Releasing queue semaphore for: {distributedLockIdentifier}.",
                            distributedLockIdentifier
                            );

                        queueSemaphore.Release();

                        Logger.LogDebug(
                            ReleasingSemaphoreEventId,
                            "Released queue semaphore for: {distributedLockIdentifier}.",
                            distributedLockIdentifier
                            );
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(
                            CheckRunEventProcessingFailedEventId,
                            ex,
                            "Failed to process check-run event for: {distributedLockIdentifier}",
                            distributedLockIdentifier
                            );

                        // Clear the semaphores.
                        semaphores.TryRemove($"trap/{distributedLockIdentifier}", out SemaphoreSlim _);
                        semaphores.TryRemove($"queue/{distributedLockIdentifier}", out SemaphoreSlim _);

                        throw ex;
                    }
                }
            }
        }
    }
}
