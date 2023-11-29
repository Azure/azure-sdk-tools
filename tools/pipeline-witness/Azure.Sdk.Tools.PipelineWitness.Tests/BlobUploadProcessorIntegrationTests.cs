using System;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Sdk.Tools.PipelineWitness.Services;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        // TODO: populate this
        private string TARGET_ACCOUNT_ID = "";
        // TODO: populate this
        private Guid TARGET_PROJECT_ID = Guid.NewGuid();

        // https://dev.azure.com/azure-sdk/internal/_build/results?buildId=3296836&view=results
        private int TARGET_BUILD_ID = 3296836;
        private string DEVOPS_PATH = "https://dev.azure.com/azure-sdk";


        public BlobUploadProcessorIntegrationTests()
        {
            var pat = Environment.GetEnvironmentVariable("AZURESDK_DEVOPS_TOKEN");
            var blobUri = Environment.GetEnvironmentVariable("AZURESDK_BLOB_SAS");

            if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(blobUri) )
            {
                VisualStudioCredentials = new VssBasicCredential("nobody", pat);
                VisualStudioConnection = new VssConnection(new Uri(DEVOPS_PATH), VisualStudioCredentials);
            }
        }

        [EnvironmentConditionalSkipFact]
        public async Task BasicBlobProcessInvokesSuccessfully()
        {
            var buildLogProvider = new BuildLogProvider(logger: null, VisualStudioConnection);
            var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AZURESDK_BLOB_SAS"));
            var buildHttpClient = new BuildHttpClient(new Uri(DEVOPS_PATH), VisualStudioCredentials);
            var testResultsClient = new TestResultsHttpClient(new Uri(DEVOPS_PATH), VisualStudioCredentials);
            var pipelineWitnessOptions = new PipelineWitnessSettings()
            {
            };

            BlobUploadProcessor processor = new BlobUploadProcessor(logger: new NullLogger<BlobUploadProcessor>(),
                logProvider: buildLogProvider,
                blobServiceClient: blobServiceClient,
                buildClient: buildHttpClient,
                testResultsClient: testResultsClient,
                options: Options.Create<PipelineWitnessSettings>(pipelineWitnessOptions),
                failureAnalyzer: new PassThroughFailureAnalyzer());

            await processor.UploadBuildBlobsAsync(TARGET_ACCOUNT_ID, TARGET_PROJECT_ID, TARGET_BUILD_ID);
        }
    }
}
