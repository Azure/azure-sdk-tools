using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.Services;
using Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace Azure.Sdk.Tools.PipelineWitness.GitHubActions
{
    public class MissingActionsWorker : PeriodicLockingBackgroundService
    {
        private readonly ILogger<MissingActionsWorker> logger;
        private readonly GitHubActionProcessor processor;
        private readonly RunCompleteQueue queue;
        private readonly IOptions<PipelineWitnessSettings> options;
        private readonly GitHubClient client;

        public MissingActionsWorker(
            ILogger<MissingActionsWorker> logger,
            GitHubActionProcessor processor,
            IAsyncLockProvider asyncLockProvider,
            ICredentialStore credentials,
            RunCompleteQueue queue,
            IOptions<PipelineWitnessSettings> options)
            : base(
                  logger,
                  asyncLockProvider,
                  lockName: "ProcessMissingGitHubActions",
                  lockDuration: options.Value.LockLeasePeriod,
                  loopDuration: options.Value.MissingBuildLoopPeriod,
                  cooldownDuration: options.Value.MissingBuildCooldownPeriod)
        {
            this.logger = logger;
            this.processor = processor;
            this.queue = queue;
            this.options = options;

            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            this.client = new GitHubClient(new ProductHeaderValue("PipelineWitness", "1.0"), credentials);
        }

        protected override async Task ProcessAsync(CancellationToken cancellationToken)
        {
            PipelineWitnessSettings settings = this.options.Value;

            // search for builds that completed within this window
            DateTimeOffset runMinTime = DateTime.UtcNow.Subtract(settings.MissingBuildLookbackPeriod);
            DateTimeOffset runMaxTime = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1));

            // search for blobs that were uploaded with a time range that overlaps the build time range
            DateTimeOffset blobMinTime = runMinTime.Subtract(TimeSpan.FromHours(1));
            DateTimeOffset blobMaxTime = runMaxTime.Add(TimeSpan.FromHours(1));

            foreach (string ownerAndRepository in settings.Repositories)
            {
                string owner = ownerAndRepository.Split('/')[0];
                string repository = ownerAndRepository.Split('/')[1];

                string[] knownBlobs = await this.processor.GetRunBlobNamesAsync(ownerAndRepository, blobMinTime, blobMaxTime, cancellationToken);

                var completedBuilds = await this.client.Actions.Workflows.Runs.List(owner, repository, new WorkflowRunsRequest
                {
                    Created = new DateRange(runMinTime, runMaxTime).ToString(),
                    Status = CheckRunStatusFilter.Completed,
                });

                var skipCount = 0;
                var enqueueCount = 0;

                foreach (WorkflowRun run in completedBuilds.WorkflowRuns)
                {
                    var buildBlobName = this.processor.GetRunBlobName(run);

                    if (knownBlobs.Contains(buildBlobName))
                    {
                        skipCount++;
                        continue;
                    }

                    var queueMessage = new RunCompleteQueueMessage
                    {
                        Owner = owner,
                        Repository = repository,
                        RunId = run.Id
                    };

                    this.logger.LogInformation("Enqueuing missing run {Repository} {RunId} for processing", ownerAndRepository, run.Id);
                    await this.queue.EnqueueMessageAsync(queueMessage);
                    enqueueCount++;
                }

                this.logger.LogInformation("Enqueued {EnqueueCount} missing runs, skipped {SkipCount} existing runs in repository {Repository}", enqueueCount, skipCount, ownerAndRepository);
            }
        }

        protected override Task ProcessExceptionAsync(Exception ex)
        {
            this.logger.LogError(ex, "Error processing missing builds");
            return Task.CompletedTask;
        }
    }
}
