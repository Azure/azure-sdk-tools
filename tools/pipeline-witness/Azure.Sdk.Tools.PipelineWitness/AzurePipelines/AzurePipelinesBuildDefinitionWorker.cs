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
        private readonly AzurePipelinesProcessor runProcessor;
        private readonly IOptions<PipelineWitnessSettings> options;

        public AzurePipelinesBuildDefinitionWorker(
            ILogger<AzurePipelinesBuildDefinitionWorker> logger,
            AzurePipelinesProcessor runProcessor,
            IAsyncLockProvider asyncLockProvider,
            IOptions<PipelineWitnessSettings> options)
            : base(
                  logger,
                  asyncLockProvider,
                  options.Value.BuildDefinitionWorker)
        {
            this.logger = logger;
            this.runProcessor = runProcessor;
            this.options = options;
        }

        protected override async Task ProcessAsync(CancellationToken cancellationToken)
        {
            var settings = this.options.Value;
            foreach (string project in settings.Projects)
            {
                await this.runProcessor.UploadBuildDefinitionBlobsAsync(settings.Account, project);
            }
        }
    }
}
