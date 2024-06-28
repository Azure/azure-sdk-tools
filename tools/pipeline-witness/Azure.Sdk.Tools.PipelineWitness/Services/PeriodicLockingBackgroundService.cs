using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    public abstract class PeriodicLockingBackgroundService : BackgroundService
    {
        private readonly ILogger logger;
        private readonly IAsyncLockProvider asyncLockProvider;
        private readonly string lockName;
        private readonly TimeSpan loopDuration;
        private readonly TimeSpan lockDuration;
        private readonly TimeSpan cooldownDuration;

        public PeriodicLockingBackgroundService(
            ILogger logger,
            IAsyncLockProvider asyncLockProvider,
            string lockName,
            TimeSpan lockDuration,
            TimeSpan loopDuration,
            TimeSpan cooldownDuration)
        {
            this.logger = logger;
            this.asyncLockProvider = asyncLockProvider;
            this.lockName = lockName;
            this.loopDuration = loopDuration;
            this.lockDuration = lockDuration;
            this.cooldownDuration = cooldownDuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (true)
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
                        var processTask = ProcessAsync(localCancellation.Token);
                        var renewLockTask = RenewLockAsync(asyncLock, this.lockDuration, localCancellation.Token);
                        await Task.WhenAny(processTask, renewLockTask);

                        // whichever task completes first, cancel the other, then wait for both to complete
                        localCancellation.Cancel();
                        await Task.WhenAll(processTask, renewLockTask);

                        // awaiting processTask will have thrown an exception if it failed

                        // if processTask completed successfully, attemt to extend the lock for the cooldown period
                        // the cooldown period prevents the process from running too frequently
                        await asyncLock.TryExtendAsync(this.cooldownDuration, stoppingToken);
                        asyncLock.ReleaseOnDispose = false;
                    }
                    else
                    {
                        this.logger.LogInformation("Lock {LockName} not acquired", this.lockName);
                    }
                }
                catch (Exception ex)
                {
                    await ProcessExceptionAsync(ex);
                }

                // Remove the time spent processing from the wait time to maintain the loop period
                TimeSpan duration = this.loopDuration - stopWatch.Elapsed;
                if (duration > TimeSpan.Zero)
                {
                    await Task.Delay(duration, stoppingToken);
                }
            }
        }

        protected abstract Task ProcessExceptionAsync(Exception ex);

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
