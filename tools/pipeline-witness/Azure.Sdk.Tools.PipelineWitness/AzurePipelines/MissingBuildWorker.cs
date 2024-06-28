using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.Services;
using Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness.AzurePipelines
{
    public class MissingBuildWorker : PeriodicLockingBackgroundService
    {
        private readonly ILogger<MissingBuildWorker> logger;
        private readonly Func<BlobUploadProcessor> runProcessorFactory;
        private readonly BuildCompleteQueue buildCompleteQueue;
        private readonly IOptions<PipelineWitnessSettings> options;
        private readonly BuildHttpClient buildClient;

        public MissingBuildWorker(
            ILogger<MissingBuildWorker> logger,
            Func<BlobUploadProcessor> runProcessorFactory,
            IAsyncLockProvider asyncLockProvider,
            VssConnection vssConnection,
            BuildCompleteQueue buildCompleteQueue,
            IOptions<PipelineWitnessSettings> options)
            : base(
                  logger,
                  asyncLockProvider,
                  lockName: "ProcessMissingBuilds",
                  lockDuration: options.Value.LockLeasePeriod,
                  loopDuration: options.Value.MissingBuildLoopPeriod,
                  cooldownDuration: options.Value.MissingBuildCooldownPeriod)
        {
            this.logger = logger;
            this.runProcessorFactory = runProcessorFactory;
            this.buildCompleteQueue = buildCompleteQueue;
            this.options = options;

            if (vssConnection == null)
            {
                throw new ArgumentNullException(nameof(vssConnection));
            }

            this.buildClient = vssConnection.GetClient<EnhancedBuildHttpClient>();
        }

        protected override async Task ProcessAsync(CancellationToken cancellationToken)
        {
            var settings = this.options.Value;
            var minFinishTime = DateTime.UtcNow.Subtract(settings.MissingBuildLookbackPeriod);
            var maxFinishTime = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));

            BlobUploadProcessor runProcessor = this.runProcessorFactory.Invoke();

            foreach (string project in settings.Projects)
            {
                string continuationToken = null;
                do
                {
                    var completedBuilds = await this.buildClient.GetBuildsAsync2(project, minFinishTime: minFinishTime, maxFinishTime: maxFinishTime, statusFilter: BuildStatus.Completed, continuationToken: continuationToken, cancellationToken: cancellationToken);

                    var skipCount = 0;
                    var enqueueCount = 0;
                    foreach (var build in completedBuilds)
                    {
                        if (await runProcessor.BuildBlobExistsAsync(build))
                        {
                            skipCount++;
                            continue;
                        }

                        var queueMessage = new BuildCompleteQueueMessage
                        {
                            Account = settings.Account,
                            ProjectId = build.Project.Id,
                            BuildId = build.Id
                        };

                        this.logger.LogInformation("Enqueuing missing build {Project} {BuildId} for processing", build.Project.Name, build.Id);
                        await this.buildCompleteQueue.EnqueueMessageAsync(queueMessage);
                        enqueueCount++;
                    }

                    this.logger.LogInformation("Enqueued {EnqueueCount} missing builds, skipped {SkipCount} existing builds in project {Project}", enqueueCount, skipCount, project);

                    continuationToken = completedBuilds.ContinuationToken;
                } while(!string.IsNullOrEmpty(continuationToken));
            }
        }

        protected override Task ProcessExceptionAsync(Exception ex)
        {
            this.logger.LogError(ex, "Error processing missing builds");
            return Task.CompletedTask;
        }
    }
}
