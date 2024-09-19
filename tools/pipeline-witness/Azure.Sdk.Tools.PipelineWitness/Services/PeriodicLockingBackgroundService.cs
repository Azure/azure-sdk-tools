using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    public abstract class PeriodicLockingBackgroundService : BackgroundService
    {
        private readonly ILogger logger;
        private readonly IAsyncLockProvider asyncLockProvider;
        private readonly bool enabled;
        private readonly string lockName;
        private readonly TimeSpan loopDuration;
        private readonly TimeSpan lockDuration;
        private readonly TimeSpan cooldownDuration;

        public PeriodicLockingBackgroundService(
            ILogger logger,
            IAsyncLockProvider asyncLockProvider,
            PeriodicProcessSettings settings)
        {
            this.logger = logger;
            this.asyncLockProvider = asyncLockProvider;
            this.enabled = settings.Enabled;
            this.lockName = settings.LockName;
            this.loopDuration = settings.LoopPeriod;
            this.lockDuration = settings.LockLeasePeriod;
            this.cooldownDuration = settings.CooldownPeriod;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (this.enabled)
            {
                Stopwatch stopWatch = Stopwatch.StartNew();

                try
                {
                    // The lock duration prevents multiple instances from running at the same time
                    // We set the lock duration shorter than the cooldown period to allow for quicker retries should a worker fail
                    await using IAsyncLock asyncLock = await this.asyncLockProvider.GetLockAsync(this.lockName, this.lockDuration, stoppingToken);

                    // if we couldn't aquire a lock, another instance is already processing or we're in the cooldown period
                    if (asyncLock != null)
                    {
                        var localCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                        // concurrently process and keep the lock alive
                        var processTask = SafelyProcessAsync(localCancellation.Token);
                        var renewLockTask = RenewLockAsync(asyncLock, this.lockDuration, localCancellation.Token);

                        Task.WaitAny([processTask, renewLockTask], stoppingToken);

                        // whichever task completes first, cancel the other, then wait for both to complete
                        localCancellation.Cancel();

                        Task.WaitAll([processTask, renewLockTask], stoppingToken);

                        // if processTask completed successfully, attemt to extend the lock for the cooldown period
                        // the cooldown period prevents the process from running too frequently
                        var (success, cooldown) = await processTask;
                        
                        await asyncLock.TryExtendAsync(cooldown, stoppingToken);
                        asyncLock.ReleaseOnDispose = false;
                    }
                    else
                    {
                        this.logger.LogInformation("Lock {LockName} not acquired", this.lockName);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException && !stoppingToken.IsCancellationRequested)
                {
                    // outside of a lock, we can't set the cooldown period on the lock, so we ignore the cooldown period in the return value
                   this.logger.LogError(ex, "Error processing");
                }

                // Remove the time spent processing from the wait time to maintain the loop period
                TimeSpan duration = this.loopDuration - stopWatch.Elapsed;
                if (duration > TimeSpan.Zero)
                {
                    await Task.Delay(duration, stoppingToken);
                }
            }
        }


        /// <summary>
        /// Call ProcessAsync and log any exception that occurs
        /// </summary>
        private async Task<(bool Success, TimeSpan PauseDuration)> SafelyProcessAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ProcessAsync(cancellationToken).ConfigureAwait(false);
                return (true, this.cooldownDuration);
            }
            catch (PauseProcessingException ex)
            {
                if (ex.InnerException != null)
                {
                    this.logger.LogError(ex.InnerException, "ProcessAsync threw exception");
                }

                return (false, ex.PauseDuration);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "ProcessAsync threw exception");
                return (false, TimeSpan.Zero);
            }
        }

        /// <summary>
        /// The main process for the defived class
        /// </summary>
        protected abstract Task ProcessAsync(CancellationToken cancellationToken);

        private async Task RenewLockAsync(IAsyncLock asyncLock, TimeSpan duration, CancellationToken cancellationToken)
        {
            // Renew the lock every half the duration until cancelled or the lock is lost
            try
            {
                while (true)
                {
                    var result = await asyncLock.TryExtendAsync(duration, cancellationToken);

                    if (!result)
                    {
                        this.logger.LogWarning("Lock lost");
                        break;
                    }

                    this.logger.LogInformation("Lock renewed");
                    await Task.Delay(duration / 2, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected, ignore cancellation exceptions
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error renewing lock");
            }
        }
    }
}
