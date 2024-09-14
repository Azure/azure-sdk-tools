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
    public class MissingGitHubActionsWorker : PeriodicLockingBackgroundService
    {
        private readonly ILogger<MissingGitHubActionsWorker> logger;
        private readonly GitHubActionProcessor processor;
        private readonly RunCompleteQueue queue;
        private readonly IOptions<PipelineWitnessSettings> options;
        private readonly GitHubClient client;

        public MissingGitHubActionsWorker(
            ILogger<MissingGitHubActionsWorker> logger,
            GitHubActionProcessor processor,
            IAsyncLockProvider asyncLockProvider,
            ICredentialStore credentials,
            RunCompleteQueue queue,
            IOptions<PipelineWitnessSettings> options)
            : base(
                  logger,
                  asyncLockProvider,
                  options.Value.MissingGitHubActionsWorker)
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

            var repositories = settings.GitHubRepositories;

            // search for builds that completed within this window
            DateTimeOffset runMinTime = DateTimeOffset.UtcNow.Subtract(settings.MissingGitHubActionsWorker.LookbackPeriod);
            DateTimeOffset runMaxTime = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(1));

            foreach (string ownerAndRepository in repositories)
            {
                try
                {
                    string owner = ownerAndRepository.Split('/')[0];
                    string repository = ownerAndRepository.Split('/')[1];

                    string[] knownBlobs = await this.processor.GetRunBlobNamesAsync(ownerAndRepository, runMinTime, runMaxTime, cancellationToken);

                    WorkflowRunsResponse listRunsResponse;

                    try
                    {
                        listRunsResponse = await this.client.Actions.Workflows.Runs.List(owner, repository, new WorkflowRunsRequest
                        {
                            Created = $"{runMinTime:o}..{runMaxTime:o}",
                            Status = CheckRunStatusFilter.Completed,
                        });
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Error listing runs for repository {Repository}", ownerAndRepository);
                        continue;
                    }
                
                    var skipCount = 0;
                    var enqueueCount = 0;

                    foreach (WorkflowRun run in listRunsResponse.WorkflowRuns)
                    {
                        var blobName = this.processor.GetRunBlobName(run);

                        if (knownBlobs.Contains(blobName, StringComparer.InvariantCultureIgnoreCase))
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
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Error processing repository {Repository}", ownerAndRepository);
                }
            }
        }

        protected override Task ProcessExceptionAsync(Exception ex)
        {
            this.logger.LogError(ex, "Error processing missing builds");
            return Task.CompletedTask;
        }
    }
}
