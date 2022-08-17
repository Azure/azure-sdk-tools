using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.WebApi;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens;
using Azure.Sdk.Tools.PipelineWitness.Configuration;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    public class AzurePipelinesBuildDefinitionWorker : BackgroundService
    {
        private readonly ILogger<AzurePipelinesBuildDefinitionWorker> logger;
        private readonly BlobUploadProcessor runProcessor;
        private readonly IOptions<PipelineWitnessSettings> options;
        private IAsyncLockProvider asyncLockProvider;

        public AzurePipelinesBuildDefinitionWorker(
            ILogger<AzurePipelinesBuildDefinitionWorker> logger,
            BlobUploadProcessor runProcessor,
            IAsyncLockProvider asyncLockProvider,
            IOptions<PipelineWitnessSettings> options)
        {
            this.logger = logger;
            this.runProcessor = runProcessor;
            this.options = options;
            this.asyncLockProvider = asyncLockProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var processEvery = TimeSpan.FromMinutes(60);

            while (true)
            {
                var stopWatch = Stopwatch.StartNew();
                var settings = this.options.Value;

                try
                {
                    await using var asyncLock = await this.asyncLockProvider.GetLockAsync("UpdateBuildDefinitions", processEvery, stoppingToken);

                    // if there's no asyncLock, this process has alread completed in the last hour
                    if (asyncLock != null)
                    {
                        foreach (var project in settings.Projects)
                        {
                            await this.runProcessor.UploadBuildDefinitionBlobsAsync(settings.Account, project);
                        }
                    }
                }
                catch(Exception ex)
                {
                    this.logger.LogError(ex, "Error processing build definitions");
                }

                var duration = settings.BuildDefinitionLoopPeriod - stopWatch.Elapsed;
                if (duration > TimeSpan.Zero)
                {
                    await Task.Delay(duration, stoppingToken);
                }
            }
        }
    }
}
