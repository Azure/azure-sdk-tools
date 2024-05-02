using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    public class AzurePipelinesBuildDefinitionWorker : BackgroundService
    {
        private readonly ILogger<AzurePipelinesBuildDefinitionWorker> logger;
        private readonly Func<BlobUploadProcessor> runProcessorFactory;
        private readonly IOptions<PipelineWitnessSettings> options;
        private readonly IAsyncLockProvider asyncLockProvider;

        public AzurePipelinesBuildDefinitionWorker(
            ILogger<AzurePipelinesBuildDefinitionWorker> logger,
            Func<BlobUploadProcessor> runProcessorFactory,
            IAsyncLockProvider asyncLockProvider,
            IOptions<PipelineWitnessSettings> options)
        {
            this.logger = logger;
            this.runProcessorFactory = runProcessorFactory;
            this.options = options;
            this.asyncLockProvider = asyncLockProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TimeSpan processEvery = TimeSpan.FromMinutes(60);

            while (true)
            {
                Stopwatch stopWatch = Stopwatch.StartNew();
                PipelineWitnessSettings settings = this.options.Value;

                try
                {
                    await using IAsyncLock asyncLock = await this.asyncLockProvider.GetLockAsync("UpdateBuildDefinitions", processEvery, stoppingToken);

                    // if there's no asyncLock, this process has already completed in the last hour
                    if (asyncLock != null)
                    {
                        BlobUploadProcessor runProcessor = this.runProcessorFactory.Invoke();
                        foreach (string project in settings.Projects)
                        {
                            await runProcessor.UploadBuildDefinitionBlobsAsync(settings.Account, project);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Error processing build definitions");
                }

                TimeSpan duration = settings.BuildDefinitionLoopPeriod - stopWatch.Elapsed;
                if (duration > TimeSpan.Zero)
                {
                    await Task.Delay(duration, stoppingToken);
                }
            }
        }
    }
}
