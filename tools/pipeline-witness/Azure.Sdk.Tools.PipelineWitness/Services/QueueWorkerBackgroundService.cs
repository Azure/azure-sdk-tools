using System;
using System.Threading;
using System.Threading.Tasks;

using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    internal abstract class QueueWorkerBackgroundService : BackgroundService
    {
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

            if (string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentException("Parameter cannot be null or whitespace", nameof(queueName));
            }

            this.queueName = queueName;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.logger.LogInformation("Starting ExecuteAsync for {TypeName}", this.GetType().Name);

            string poisonQueueName = $"{this.queueName}-poison";

            QueueClient queueClient = this.queueServiceClient.GetQueueClient(this.queueName);
            QueueClient poisonQueueClient = this.queueServiceClient.GetQueueClient(poisonQueueName);

            await queueClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
            await poisonQueueClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

            while (true)
            {
                var loopTelementy = new RequestTelemetry
                { 
                    Name = "MessageLoopIteration", 
                    Properties = { ["QueueName"] = queueClient.Name }
                };

                using var loopOperation = this.telemetryClient.StartOperation(loopTelementy);

                PipelineWitnessSettings options = this.options.CurrentValue;

                this.logger.LogDebug("Getting next message from queue {QueueName}", queueClient.Name);

                TimeSpan pauseDuration = TimeSpan.Zero;

                try
                {
                    // We consider a message leased when it's made invisible in the queue and the current process has a
                    // valid PopReceipt for the message. The PopReceipt is used to perform subsequent operations on the
                    // "leased" message.
                    QueueMessage message = await queueClient.ReceiveMessageAsync(options.MessageLeasePeriod, stoppingToken);

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

                    using IOperationHolder<RequestTelemetry> messageOperation = this.telemetryClient.StartOperation(new RequestTelemetry
                    {
                        Name = "ProcessMessage",
                        Properties = { ["MessageId"] = message.MessageId }
                    });

                    try
                    {
                        this.logger.LogDebug("The queue returned a message.\n  Queue: {Queue}\n  Message: {MessageId}\n  Dequeue Count: {DequeueCount}\n  Pop Receipt: {PopReceipt}", queueClient.Name, message.MessageId, message.DequeueCount, message.PopReceipt);

                        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                        // Because processing a message may take longer than our initial lease period, we want to continually
                        // renew our lease until processing completes.
                        var renewTask = RenewMessageLeaseAsync(queueClient, message, cts.Token);
                        var processTask = SafelyProcessMessageAsync(message, cts.Token);

                        Task.WaitAny([renewTask, processTask], CancellationToken.None);

                        cts.Cancel();

                        // if the renew task doesn't complete successfully, we can't trust the PopReceipt on the message and must abort.
                        string latestPopReceipt = await renewTask;
                        var result = await processTask;
                        pauseDuration = result.pauseDuration;

                        if (result.Success)
                        {
                            this.logger.LogDebug("Message processed successfully. Removing message from queue.\n  MessageId: {MessageId}\n  Queue: {QueueName}\n  PopReceipt: {PopReceipt}", message.MessageId, queueClient.Name, latestPopReceipt);
                            await queueClient.DeleteMessageAsync(message.MessageId, latestPopReceipt, stoppingToken);
                            messageOperation.Telemetry.Success = true;
                        }
                        else
                        {
                            messageOperation.Telemetry.Success = false;
                            if (message.DequeueCount > options.MaxDequeueCount)
                            {
                                this.logger.LogError("Message {MessageId} exceeded maximum dequeue count. Moving to poison queue {QueueName}", message.MessageId, poisonQueueClient.Name);
                                await poisonQueueClient.SendMessageAsync(message.Body, cancellationToken: stoppingToken);
                                this.logger.LogDebug("Removing message from queue.\n  MessageId: {MessageId}\n  Queue: {QueueName}\n  PopReceipt: {PopReceipt}", message.MessageId, queueClient.Name, latestPopReceipt);
                                await queueClient.DeleteMessageAsync(message.MessageId, latestPopReceipt, stoppingToken);
                            }
                            else
                            {
                                // Use message.DequeueCount for exponential backoff
                                var sleepMultiplier = Math.Pow(2, Math.Max(message.DequeueCount - 1, 0));
                                var sleepPeriod = TimeSpan.FromSeconds(sleepMultiplier * options.MessageErrorSleepPeriod.TotalSeconds);

                                this.logger.LogError("Resetting message visibility timeout to {SleepPeriod}.\n  MessageId: {MessageId}\n  Queue: {QueueName}\n  PopReceipt: {PopReceipt}", sleepPeriod, message.MessageId, queueClient.Name, latestPopReceipt);
                                await queueClient.UpdateMessageAsync(message.MessageId, latestPopReceipt, message.Body, sleepPeriod, cancellationToken: stoppingToken);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Exception thrown while procesing queue message.");
                        messageOperation.Telemetry.Success = false;
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Exception thrown while procesing message loop.");
                    pauseDuration = options.MessageErrorSleepPeriod;
                    loopOperation.Telemetry.Success = false;
                }


                if (pauseDuration != TimeSpan.Zero)
                {
                    this.logger.LogWarning("Pause in processing requested. Waiting {PauseDuration}.", pauseDuration);
                    await Task.Delay(pauseDuration, stoppingToken);
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
            TimeSpan leasePeriod = this.options.CurrentValue.MessageLeasePeriod;
            TimeSpan halfLife = new(leasePeriod.Ticks / 2);
            string queueName = queueClient.Name;
            string messageId = message.MessageId;
            string popReceipt = message.PopReceipt;
            DateTimeOffset? nextVisibleOn = message.NextVisibleOn;

            try
            {
                while (true)
                {
                    // We extend the lease after half of the lease period has expired.
                    // For a 30 second MessageLeasePeriod, every 15 seconds,
                    // we'll set the message to invisible for 30 seconds.
                    await Task.Delay(halfLife, cancellationToken);

                    this.logger.LogDebug("Extending visibility timeout for message.\n  Queue: {Queue}\n  Message: {MessageId}\n  Pop Receipt: {PopReceipt}\n  Visible in: {VisibleIn}", queueName, messageId, popReceipt, nextVisibleOn - DateTimeOffset.UtcNow);
                    UpdateReceipt receipt = await queueClient.UpdateMessageAsync(messageId, popReceipt, visibilityTimeout: leasePeriod, cancellationToken: cancellationToken);

                    string oldPopReceipt = popReceipt;
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
        private async Task<(bool Success, TimeSpan pauseDuration)> SafelyProcessMessageAsync(QueueMessage message, CancellationToken cancellationToken)
        {
            try
            {
                await ProcessMessageAsync(message, cancellationToken).ConfigureAwait(false);
                return (true, TimeSpan.Zero);
            }
            catch(PauseProcessingException ex)
            {
                // The processor can throw a PauseProcessing exception that will stop the message loop for a period of time.
                // This is useful when the processor detects a condition that will not be resolved by reprocessing the message, e.g. rate limiting.
                if(ex.InnerException != null)
                {
                    this.logger.LogError(ex.InnerException, "ProcessMessageAsync threw exception");
                }

                return (false, ex.PauseDuration);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "ProcessMessageAsync threw exception");
                return (false, TimeSpan.Zero);
            }
        }
    }
}
