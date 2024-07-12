using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.Services;
using Azure.Sdk.Tools.PipelineWitness.Services.WorkTokens;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.TestResults.WebApi;

namespace Azure.Sdk.Tools.PipelineWitness.AzurePipelines
{
    public class AzurePipelinesBuildDefinitionWorker : PeriodicLockingBackgroundService
    {
        private readonly ILogger<AzurePipelinesBuildDefinitionWorker> logger;
        private readonly Func<BlobUploadProcessor> runProcessorFactory;
        private readonly IOptions<PipelineWitnessSettings> options;

        public AzurePipelinesBuildDefinitionWorker(
            ILogger<AzurePipelinesBuildDefinitionWorker> logger,
            Func<BlobUploadProcessor> runProcessorFactory,
            IAsyncLockProvider asyncLockProvider,
            IOptions<PipelineWitnessSettings> options)
            : base(
                  logger,
                  asyncLockProvider,
                  lockName: "UpdateBuildDefinitions",
                  lockDuration: options.Value.LockLeasePeriod,
                  loopDuration: options.Value.BuildDefinitionLoopPeriod,
                  cooldownDuration: options.Value.BuildDefinitionCooldownPeriod)
        {
            this.logger = logger;
            this.runProcessorFactory = runProcessorFactory;
            this.options = options;
        }

        protected override async Task ProcessAsync(CancellationToken cancellationToken)
        {
            var settings = this.options.Value;
            BlobUploadProcessor runProcessor = this.runProcessorFactory.Invoke();
            foreach (string project in settings.Projects)
            {
                await runProcessor.UploadBuildDefinitionBlobsAsync(settings.Account, project, cancellationToken);
            }
        }

        protected override Task ProcessExceptionAsync(Exception ex)
        {
            this.logger.LogError(ex, "Error processing build definitions");
            return Task.CompletedTask;
        }
    }
}
