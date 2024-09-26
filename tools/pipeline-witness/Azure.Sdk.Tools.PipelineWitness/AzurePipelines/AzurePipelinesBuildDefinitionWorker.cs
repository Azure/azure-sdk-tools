using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.Services;
using Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Sdk.Tools.PipelineWitness.AzurePipelines
{
    public class AzurePipelinesBuildDefinitionWorker : PeriodicLockingBackgroundService
    {
        private readonly ILogger<AzurePipelinesBuildDefinitionWorker> logger;
        private readonly Func<AzurePipelinesProcessor> processorFactory;
        private readonly IOptions<PipelineWitnessSettings> options;

        public AzurePipelinesBuildDefinitionWorker(
            ILogger<AzurePipelinesBuildDefinitionWorker> logger,
            Func<AzurePipelinesProcessor> processorFactory,
            IAsyncLockProvider asyncLockProvider,
            IOptions<PipelineWitnessSettings> options)
            : base(
                  logger,
                  asyncLockProvider,
                  options.Value.BuildDefinitionWorker)
        {
            this.logger = logger;
            this.processorFactory = processorFactory;
            this.options = options;
        }

        protected override async Task ProcessAsync(CancellationToken cancellationToken)
        {
            var settings = this.options.Value;
            var processor = this.processorFactory();
            foreach (string project in settings.Projects)
            {
                await processor.UploadBuildDefinitionBlobsAsync(settings.Account, project);
            }
        }
    }
}
