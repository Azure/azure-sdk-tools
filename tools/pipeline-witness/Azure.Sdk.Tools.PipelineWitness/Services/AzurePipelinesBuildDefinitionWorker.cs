using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.WebApi;
using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    public class AzurePipelinesBuildDefinitionWorker : BackgroundService
    {
        private readonly ILogger<AzurePipelinesBuildDefinitionWorker> logger;
        private readonly BlobUploadProcessor runProcessor;
        private readonly IOptions<PipelineWitnessSettings> options;

        public AzurePipelinesBuildDefinitionWorker(
            ILogger<AzurePipelinesBuildDefinitionWorker> logger,
            BlobUploadProcessor runProcessor,
            IOptions<PipelineWitnessSettings> options)
        {
            this.logger = logger;
            this.runProcessor = runProcessor;
            this.options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (true)
            {
                var stopWatch = Stopwatch.StartNew();
                var settings = this.options.Value;

                foreach (var project in settings.Projects)
                {
                    await this.runProcessor.UploadBuildDefinitionBlobsAsync(settings.Account, project);
                }

                var duration = settings.BuildDefinitionLoopPeriod - stopWatch.Elapsed;
                if (duration > TimeSpan.Zero)
                {
                    await Task.Delay(duration);
                }
            }
        }
    }
}
