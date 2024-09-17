using System;
using System.Linq;
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
    public class MissingAzurePipelineRunsWorker : PeriodicLockingBackgroundService
    {
        private readonly ILogger<MissingAzurePipelineRunsWorker> logger;
        private readonly AzurePipelinesProcessor runProcessor;
        private readonly BuildCompleteQueue buildCompleteQueue;
        private readonly IOptions<PipelineWitnessSettings> options;
        private readonly EnhancedBuildHttpClient buildClient;

        public MissingAzurePipelineRunsWorker(
            ILogger<MissingAzurePipelineRunsWorker> logger,
            AzurePipelinesProcessor runProcessor,
            IAsyncLockProvider asyncLockProvider,
            VssConnection vssConnection,
            BuildCompleteQueue buildCompleteQueue,
            IOptions<PipelineWitnessSettings> options)
            : base(
                  logger,
                  asyncLockProvider,
                  options.Value.MissingPipelineRunsWorker)
        {
            this.logger = logger;
            this.runProcessor = runProcessor;
            this.buildCompleteQueue = buildCompleteQueue;
            this.options = options;

            ArgumentNullException.ThrowIfNull(vssConnection);

            this.buildClient = vssConnection.GetClient<EnhancedBuildHttpClient>();
        }

        protected override async Task ProcessAsync(CancellationToken cancellationToken)
        {
            var settings = this.options.Value;

            // search for builds that completed within this window
            var buildMinTime = DateTimeOffset.UtcNow.Subtract(settings.MissingPipelineRunsWorker.LookbackPeriod);
            var buildMaxTime = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(1));

            foreach (string project in settings.Projects)
            {
                var knownBlobs = await this.runProcessor.GetBuildBlobNamesAsync(project, buildMinTime, buildMaxTime, cancellationToken);

                string continuationToken = null;
                do
                {
                    var completedBuilds = await this.buildClient.GetBuildsAsync2(
                        project,
                        minFinishTime: buildMinTime.DateTime,
                        maxFinishTime: buildMaxTime.DateTime,
                        statusFilter: BuildStatus.Completed,
                        continuationToken: continuationToken,
                        cancellationToken: cancellationToken);

                    var skipCount = 0;
                    var enqueueCount = 0;
                    foreach (var build in completedBuilds)
                    {
                        var blobName = this.runProcessor.GetBuildBlobName(build);

                        if (knownBlobs.Contains(blobName, StringComparer.InvariantCultureIgnoreCase))
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
    }
}
