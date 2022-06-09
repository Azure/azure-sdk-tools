using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    internal abstract class QueueWorkerBackgroundService : BackgroundService
    {
        private const string ActivitySourceName = "Azure.Sdk.Tools.PipelineWitness.Queue";
        private static readonly ActivitySource activitySource = new ActivitySource(ActivitySourceName);

        private readonly ILogger logger;
        private readonly QueueClient queueClient;
        private readonly QueueClient poisonQueueClient;
        private readonly TelemetryClient telemetryClient;
        private readonly IOptions<PipelineWitnessSettings> options;

        public QueueWorkerBackgroundService(
            ILogger logger,
            QueueClient queueClient,
            QueueClient poisonQueueClient,
            TelemetryClient telemetryClient,
            IOptions<PipelineWitnessSettings> options)
        {
            this.logger = logger;
            this.telemetryClient = telemetryClient;
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.queueClient = queueClient;
            this.poisonQueueClient = poisonQueueClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await this.queueClient.CreateIfNotExistsAsync();
            await this.poisonQueueClient.CreateIfNotExistsAsync();

            this.logger.LogInformation("Starting ExecuteAsync {TypeName}", this.GetType().Name);

            while (true)
            {
                var options = this.options.Value;

                this.logger.LogDebug("Getting next message from queue {QueueName}", this.queueClient.Name);

                try
                {
                    // We consider a message leased when it's made invisible in the queue and the current process has a
                    // valid PopReceipt for the message. The PopReceipt is used to perform subsequent operations on the
                    // "leased" message.
                    QueueMessage message = await this.queueClient.ReceiveMessageAsync(options.MessageLeasePeriod);

                    if(message == null)
                    {
                        this.logger.LogDebug("The queue returned no message. Waiting {Delay}.", options.EmptyQueuePollDelay);
                        await Task.Delay(options.EmptyQueuePollDelay, stoppingToken);
                        continue;
                    }

                    if (message.InsertedOn.HasValue)
                    {
                        this.telemetryClient.TrackMetric(new MetricTelemetry
                        {
                            Name = $"{this.queueClient.Name} MessageLatencyMs",
                            Sum = DateTimeOffset.Now.Subtract(message.InsertedOn.Value).TotalMilliseconds
                        });
                    }

                    using (var activity = activitySource.CreateActivity("ProcessMessage", ActivityKind.Internal) ?? new Activity("ProcessMessage"))
                    {
                        activity?.AddBaggage("QueueName", queueClient.Name);
                        activity?.AddBaggage("MessageId", message.MessageId);
                    
                        using var operation = this.telemetryClient.StartOperation<RequestTelemetry>(activity);

                        try
                        { 
                            this.logger.LogDebug("The queue returned messsage {MessageId}, dequeue count {DequeueCount}.", message.MessageId, message.DequeueCount);

                            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                            // Because processing a message may take longer than our initial lease period, we want to continually
                            // renew our lease until processing completes.
                            var renewTask = RenewMessageLeaseAsync(message, cts.Token);
                            var processTask = SafelyProcessMessageAsync(message, cts.Token);

                            var tasks = new[] { renewTask, processTask };

                            Task.WaitAny(tasks, CancellationToken.None);

                            cts.Cancel();

                            Task.WaitAll(tasks, CancellationToken.None);

                            if (processTask.IsCompletedSuccessfully && processTask.Result == true)
                            {
                                await this.queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
                                activity?.SetStatus(ActivityStatusCode.Ok);
                                operation.Telemetry.Success = true;
                            }
                            else
                            {
                                activity?.SetStatus(ActivityStatusCode.Error);
                                operation.Telemetry.Success = false;
                                if (message.DequeueCount > options.MaxDequeueCount)
                                {
                                    await this.poisonQueueClient.SendMessageAsync(message.Body, cancellationToken: stoppingToken);
                                    await this.queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
                                }
                                else
                                {
                                    await this.queueClient.UpdateMessageAsync(message.MessageId, message.PopReceipt, message.Body, options.MessageErrorSleepPeriod, cancellationToken: stoppingToken);
                                }
                            }
                        }
                        catch
                        {
                            activity?.SetStatus(ActivityStatusCode.Error);
                            operation.Telemetry.Success = false;
                        }
                    }
                }
                catch
                {
                    await Task.Delay(options.MessageErrorSleepPeriod, stoppingToken);
                }
            }
        }

        /// <summary>
        /// Process a single message from the queue.
        /// </summary>
        internal abstract Task ProcessMessageAsync(QueueMessage message, CancellationToken cancellationToken);

        /// <summary>
        /// Continually renew the message lease while processing is occuring in another task.
        /// </summary>
        /// <returns>true if renewal loop stops because of external cancellation, otherwise false.</returns>
        private async Task<bool> RenewMessageLeaseAsync(QueueMessage message, CancellationToken cancellationToken)
        {
            try
            {
                var leasePeriod = this.options.Value.MessageLeasePeriod;
                var halfLife = new TimeSpan(leasePeriod.Ticks / 2);
                var popReceipt = message.PopReceipt;

                while (true)
                {
                    // We extend the lease after half of the lease period has expired.
                    // For a 30 second MessageLeasePeriod, every 15 seconds, we'll set the message to invisible
                    // for 30 seconds.                    
                    await Task.Delay(halfLife, cancellationToken);

                    this.logger.LogDebug("Extending visibility timeout for message: {MessageId}", message.MessageId);
                    UpdateReceipt receipt = await this.queueClient.UpdateMessageAsync(message.MessageId, popReceipt, message.Body, visibilityTimeout: leasePeriod, cancellationToken: cancellationToken);
                    popReceipt = receipt.PopReceipt;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // It's normal for RenewMassageAsync to be cancelled when ProcessMessageAsync completes.
                // Log the cancellation and return success
                this.logger.LogDebug("RenewMessageAsync received a cancellation request");
                return true;
            }
            catch (Exception ex)
            {
                // It's not normal for RenewMassageAsync to throw any other exception
                // Log the exception and return failure
                this.logger.LogError(ex, "Unexpected exception when trying to renew a message lease");
                return false;
            }
        }

        /// <summary>
        /// Call ProcessMessageAsync and log any exception that occurs
        /// </summary>
        /// <returns>true if ProcessMessageAsync completes successfully, otherwise false</returns>
        private async Task<bool> SafelyProcessMessageAsync(QueueMessage message, CancellationToken cancellationToken)
        {
            try
            {
                await ProcessMessageAsync(message, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "ProcessMessageAsync threw exception");
                return false;
            }
        }
    }
}
