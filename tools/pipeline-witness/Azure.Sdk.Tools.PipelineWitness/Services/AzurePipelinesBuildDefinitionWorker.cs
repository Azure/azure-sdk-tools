using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using Azure.Sdk.Tools.PipelineWitness.ApplicationInsights;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    public class AzurePipelinesBuildDefinitionWorker : BackgroundService
    {
        private readonly TelemetryClient telemetryClient;
        private readonly BlobUploadProcessor blobUploadProcessor;
        private readonly IOptions<PipelineWitnessSettings> options;

        public AzurePipelinesBuildDefinitionWorker(
            TelemetryClient telemetryClient,
            BlobUploadProcessor blobUploadProcessor,
            IOptions<PipelineWitnessSettings> options)
        {
            this.telemetryClient = telemetryClient;
            this.blobUploadProcessor = blobUploadProcessor;
            this.options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (true)
            {
                var stopWatch = Stopwatch.StartNew();
                var settings = this.options.Value;

                try
                {
                    await this.telemetryClient.TraceAsync(() => ProcessBuildDefinitionsAsync(settings));
                }
                catch (Exception ex)
                {
                    this.telemetryClient.TrackException(ex);
                }

                var duration = settings.BuildDefinitionLoopPeriod - stopWatch.Elapsed;

                if (duration > TimeSpan.Zero)
                {
                    await Task.Delay(duration, stoppingToken);
                }
            }
        }

        private async Task ProcessBuildDefinitionsAsync(PipelineWitnessSettings settings)
        {
            foreach (var project in settings.Projects)
            {
                await this.blobUploadProcessor.UploadBuildDefinitionBlobsAsync(settings.Account, project);
            }
        }
    }
}
