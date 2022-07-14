using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineWitness.Services
{
    public class AzurePipelinesBuildDefinitionWorker : BackgroundService
    {
        private readonly BlobUploadProcessor _runProcessor;
        private readonly IOptions<PipelineWitnessSettings> _options;

        public AzurePipelinesBuildDefinitionWorker(
            BlobUploadProcessor runProcessor,
            IOptions<PipelineWitnessSettings> options)
        {
            _runProcessor = runProcessor;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (true)
            {
                var stopWatch = Stopwatch.StartNew();
                var settings = _options.Value;

                foreach (var project in settings.Projects)
                {
                    await _runProcessor.UploadBuildDefinitionBlobsAsync(settings.Account, project);
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
