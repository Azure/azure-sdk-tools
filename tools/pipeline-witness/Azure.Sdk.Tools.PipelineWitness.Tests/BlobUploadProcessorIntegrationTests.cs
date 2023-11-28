using System;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.Services;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Account;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.TestResults.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Xunit;

namespace Azure.Sdk.Tools.PipelineWitness.Tests
{
    public class BlobUploadProcessorIntegrationTests
    {
        private VssCredentials VisualStudioCredentials;
        private VssConnection VisualStudioConnection;
        private string TARGET_ACCOUNT_ID = "";
        private Guid TARGET_PROJECT_ID = Guid.NewGuid();
        private int TARGET_BUILD_ID = 3296836; // https://dev.azure.com/azure-sdk/internal/_build/results?buildId=3296836&view=results


        public BlobUploadProcessorIntegrationTests()
        {
            var pat = Environment.GetEnvironmentVariable("AZURESDK_DEVOPS_PAT");
            var blobUri = Environment.GetEnvironmentVariable("AZURESDK_BLOB_SAS");

;

            if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(blobUri) )
            {
                VisualStudioCredentials = new VssBasicCredential("nobody", pat);
                VisualStudioConnection = new VssConnection(new Uri("https://dev.azure.com/azure-sdk"), VisualStudioCredentials);
            }
        }

        [EnvironmentConditionalSkipFact]
        public async Task BasicBlobProcessInvokesSuccessfully()
        {
            var buildLogProvider = new BuildLogProvider(logger: null, VisualStudioConnection);
            var blobServiceClient = new BlobServiceClient("BLOB CONTAINER CONNECTION STRING");
            var buildHttpClient = new BuildHttpClient(new Uri("https://BASE/URI/TO/DEVOPS"), VisualStudioCredentials);
            var testResultsClient = new TestResultsHttpClient(new Uri("https://BASE/URI/TO/DEVOPS"), VisualStudioCredentials);
            var pipelineWitnessOptions = new PipelineWitnessSettings()
            {
                // TODO: fill in details here.
            };

            BlobUploadProcessor processor = new BlobUploadProcessor(logger: new NullLogger<BlobUploadProcessor>(),
                logProvider: buildLogProvider,
                blobServiceClient: blobServiceClient,
                buildClient: buildHttpClient,
                testResultsClient: testResultsClient,
                options: (Microsoft.Extensions.Options.IOptions<PipelineWitnessSettings>)pipelineWitnessOptions,
                failureAnalyzer: new PassThroughFailureAnalyzer());

            await processor.UploadBuildBlobsAsync(TARGET_ACCOUNT_ID, TARGET_PROJECT_ID, TARGET_BUILD_ID);
        }
    }
}
