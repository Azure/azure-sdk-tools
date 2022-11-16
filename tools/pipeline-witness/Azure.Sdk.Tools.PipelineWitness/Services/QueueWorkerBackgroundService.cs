using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Azure.Sdk.Tools.PipelineWitness.Configuration;
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
        private readonly QueueServiceClient queueServiceClient;
        private readonly string queueName;
        private readonly TelemetryClient telemetryClient;
        private readonly IOptionsMonitor<PipelineWitnessSettings> options;

        public QueueWorkerBackgroundService(
            ILogger logger,
            TelemetryClient telemetryClient,
            QueueServiceClient queueServiceClient,
            string queueName,
            IOptionsMonitor<PipelineWitnessSettings> options)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            this.queueServiceClient = queueServiceClient ?? throw new ArgumentNullException(nameof(options));
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            if(string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentException("Parameter cannot be null or whitespace", nameof(queueName));
            }

            this.queueName = queueName;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.logger.LogInformation("Starting ExecuteAsync for {TypeName}", this.GetType().Name);

            var poisonQueueName = $"{this.queueName}-poison";

            var queueClient = this.queueServiceClient.GetQueueClient(this.queueName);
            var poisonQueueClient = this.queueServiceClient.GetQueueClient(poisonQueueName);

            await queueClient.CreateIfNotExistsAsync();
            await poisonQueueClient.CreateIfNotExistsAsync();

            while (true)
            {
                using var loopActivity = activitySource.CreateActivity("MessageLoopIteration", ActivityKind.Internal) ?? new Activity("MessageLoopIteration");
                loopActivity?.AddBaggage("QueueName", queueClient.Name);
                
                using var loopOperation = this.telemetryClient.StartOperation<RequestTelemetry>(loopActivity);

                var options = this.options.CurrentValue;

                this.logger.LogDebug("Getting next message from queue {QueueName}", queueClient.Name);

                try
                {
                    // We consider a message leased when it's made invisible in the queue and the current process has a
                    // valid PopReceipt for the message. The PopReceipt is used to perform subsequent operations on the
                    // "leased" message.
                    QueueMessage message = await queueClient.ReceiveMessageAsync(options.MessageLeasePeriod);

                    if (message == null)
                    {
                        this.logger.LogDebug("The queue returned no message. Waiting {Delay}.", options.EmptyQueuePollDelay);
                        await Task.Delay(options.EmptyQueuePollDelay, stoppingToken);
                        continue;
                    }

                    if (message.InsertedOn.HasValue)
                    {
                        this.telemetryClient.TrackMetric(new MetricTelemetry
                        {
                            Name = $"{this.queueName} MessageLatencyMs",
                            Sum = DateTimeOffset.Now.Subtract(message.InsertedOn.Value).TotalMilliseconds
                        });
                    }

                    using (var activity = activitySource.CreateActivity("ProcessMessage", ActivityKind.Internal) ?? new Activity("ProcessMessage"))
                    {
                        activity?.AddBaggage("MessageId", message.MessageId);

                        using var operation = this.telemetryClient.StartOperation<RequestTelemetry>(activity);

                        try
                        {
                            this.logger.LogDebug("The queue returned a message.\n  Queue: {Queue}\n  Message: {MessageId}\n  Dequeue Count: {DequeueCount}\n  Pop Receipt: {PopReceipt}", queueClient.Name, message.MessageId, message.DequeueCount, message.PopReceipt);

                            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                            // Because processing a message may take longer than our initial lease period, we want to continually
                            // renew our lease until processing completes.
                            var renewTask = RenewMessageLeaseAsync(queueClient, message, cts.Token);
                            var processTask = SafelyProcessMessageAsync(message, cts.Token);

                            var tasks = new Task[] { renewTask, processTask };

                            Task.WaitAny(tasks, CancellationToken.None);

                            cts.Cancel();

                            Task.WaitAll(tasks, CancellationToken.None);

                            // if the renew task doesn't complete successfully, we can't trust the PopReceipt on the message and must abort.
                            var latestPopReceipt = await renewTask;

                            if (processTask.IsCompletedSuccessfully && processTask.Result == true)
                            {
                                this.logger.LogDebug("Message processed successfully. Removing message from queue.\n  MessageId: {MessageId}\n  Queue: {QueueName}\n  PopReceipt: {PopReceipt}", message.MessageId, queueClient.Name, latestPopReceipt);
                                await queueClient.DeleteMessageAsync(message.MessageId, latestPopReceipt, stoppingToken);
                                activity?.SetStatus(ActivityStatusCode.Ok);
                                operation.Telemetry.Success = true;
                            }
                            else
                            {
                                activity?.SetStatus(ActivityStatusCode.Error);
                                operation.Telemetry.Success = false;
                                if (message.DequeueCount > options.MaxDequeueCount)
                                {
                                    this.logger.LogError("Message {MessageId} exceeded maximum dequeue count. Moving to poison queue {QueueName}", message.MessageId, poisonQueueClient.Name);
                                    await poisonQueueClient.SendMessageAsync(message.Body, cancellationToken: stoppingToken);
                                    this.logger.LogDebug("Removing message from queue.\n  MessageId: {MessageId}\n  Queue: {QueueName}\n  PopReceipt: {PopReceipt}", message.MessageId, queueClient.Name, latestPopReceipt);
                                    await queueClient.DeleteMessageAsync(message.MessageId, latestPopReceipt, stoppingToken);
                                }
                                else
                                {
                                    this.logger.LogError("Resetting message visibility timeout to {SleepPeriod}.\n  MessageId: {MessageId}\n  Queue: {QueueName}\n  PopReceipt: {PopReceipt}", options.MessageErrorSleepPeriod, message.MessageId, queueClient.Name, latestPopReceipt);
                                    await queueClient.UpdateMessageAsync(message.MessageId, latestPopReceipt, message.Body, options.MessageErrorSleepPeriod, cancellationToken: stoppingToken);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            this.logger.LogError(ex, "Exception thrown while procesing queue message.");
                            activity?.SetStatus(ActivityStatusCode.Error);
                            operation.Telemetry.Success = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Exception thrown while procesing message loop.");
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
        /// <returns>the current pop receipt (optimistic concurrency control) for the message.</returns>
        private async Task<string> RenewMessageLeaseAsync(QueueClient queueClient, QueueMessage message, CancellationToken cancellationToken)
        {
            var leasePeriod = this.options.CurrentValue.MessageLeasePeriod;
            var halfLife = new TimeSpan(leasePeriod.Ticks / 2);
            var queueName = queueClient.Name;
            var messageId = message.MessageId;
            var popReceipt = message.PopReceipt;
            var nextVisibleOn = message.NextVisibleOn;

            try
            {
                while (true)
                {
                    // We extend the lease after half of the lease period has expired.
                    // For a 30 second MessageLeasePeriod, every 15 seconds, we'll set the message to invisible
                    // for 30 seconds.                    
                    await Task.Delay(halfLife, cancellationToken);

                    this.logger.LogDebug("Extending visibility timeout for message.\n  Queue: {Queue}\n  Message: {MessageId}\n  Pop Receipt: {PopReceipt}\n  Visible in: {VisibleIn}", queueName, messageId, popReceipt, nextVisibleOn - DateTimeOffset.UtcNow);
                    UpdateReceipt receipt = await queueClient.UpdateMessageAsync(messageId, popReceipt, visibilityTimeout: leasePeriod, cancellationToken: cancellationToken);

                    var oldPopReceipt = popReceipt;
                    popReceipt = receipt.PopReceipt;
                    nextVisibleOn = receipt.NextVisibleOn;

                    this.logger.LogDebug("Message visibility extended. Queue: {Queue}\n  Message: {MessageId}\n  Old pop receipt: {OldPopReceipt}\n  New pop receipt: {NewPopReceipt}\n  Visible in: {VisibleIn}", queueName, messageId, oldPopReceipt, popReceipt, nextVisibleOn - DateTimeOffset.UtcNow);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // It's normal for RenewMassageAsync to be cancelled when ProcessMessageAsync completes.
                // Log the cancellation and return success
                this.logger.LogDebug("RenewMessageAsync received a cancellation request. Message: {MessageId}\n  Pop Receipt: {PopReceipt}", messageId, popReceipt);
            }
            catch (Exception ex)
            {
                // It's not normal for RenewMassageAsync to throw any exception other than OperationCanceledException.
                // Log the exception and rethrow
                this.logger.LogError(ex, "Unexpected exception when trying to renew message lease. Queue: {Queue}\n  Message: {MessageId}\n  Pop Receipt: {PopReceipt}", queueName, messageId, popReceipt);
                throw;
            }

            return popReceipt;
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
