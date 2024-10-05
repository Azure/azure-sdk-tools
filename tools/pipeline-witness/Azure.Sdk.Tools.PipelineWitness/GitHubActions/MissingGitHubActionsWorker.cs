using System;
using System.Linq;
using System.Text.Json;
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
        private readonly GitHubClientFactory clientFactory;

        public MissingGitHubActionsWorker(
            ILogger<MissingGitHubActionsWorker> logger,
            GitHubActionProcessor processor,
            IAsyncLockProvider asyncLockProvider,
            RunCompleteQueue queue,
            GitHubClientFactory clientFactory,
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
            this.clientFactory = clientFactory;
        }

        protected override async Task ProcessAsync(CancellationToken cancellationToken)
        {
            PipelineWitnessSettings settings = this.options.Value;

            var repositories = settings.GitHubRepositories;

            // search for builds that completed within this window
            DateTimeOffset runMinTime = DateTimeOffset.UtcNow.Subtract(settings.MissingGitHubActionsWorker.LookbackPeriod);
            DateTimeOffset runMaxTime = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(1));

            try
            {
                foreach (string ownerAndRepository in repositories)
                {
                    try
                    {
                        this.logger.LogInformation("Processing missing builds for {Repository}", ownerAndRepository);
                        await ProcessRepositoryAsync(ownerAndRepository, runMinTime, runMaxTime, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not RateLimitExceededException)
                    {
                        this.logger.LogError(ex, "Error processing repository {Repository}", ownerAndRepository);
                    }
                }
            }
            catch(RateLimitExceededException ex)
            {
                try
                {
                    var client = await this.clientFactory.CreateGitHubClientAsync();
                    var rateLimit = await client.RateLimit.GetRateLimits();
                    this.logger.LogInformation("Rate limit details: {RateLimit}", JsonSerializer.Serialize(rateLimit.Resources));
                }
                catch (Exception rateLimitException)
                {
                    this.logger.LogError(rateLimitException, "Error logging rate limit details");
                }

                var resetRemaining = ex.Reset - DateTimeOffset.UtcNow;
                this.logger.LogError("Rate limit exceeded. Pausing processing for {RateLimitReset}", resetRemaining);
                throw new PauseProcessingException(resetRemaining);
            }
        }

        private async Task ProcessRepositoryAsync(string ownerAndRepository, DateTimeOffset runMinTime, DateTimeOffset runMaxTime, CancellationToken cancellationToken)
        {
            string owner = ownerAndRepository.Split('/')[0];
            string repository = ownerAndRepository.Split('/')[1];

            string[] knownBlobs = await processor.GetRunBlobNamesAsync(ownerAndRepository, runMinTime, runMaxTime, cancellationToken);

            WorkflowRunsResponse listRunsResponse;

            try
            {
                var client = await this.clientFactory.CreateGitHubClientAsync(owner, repository);
                listRunsResponse = await client.Actions.Workflows.Runs.List(owner, repository, new WorkflowRunsRequest
                {
                    Created = $"{runMinTime:o}..{runMaxTime:o}",
                    Status = CheckRunStatusFilter.Completed,
                });
            }
            catch (NotFoundException)
            {
                this.logger.LogWarning("Repository {Repository} not found", ownerAndRepository);
                return;
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
    }
}
