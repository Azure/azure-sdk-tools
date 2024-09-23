using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PipelineWitness.AzurePipelines;
using Azure.Sdk.Tools.PipelineWitness.Configuration;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Xunit;

namespace Azure.Sdk.Tools.PipelineWitness.Tests
{
    public class BlobUploadProcessorIntegrationTests
    {
        private const string TARGET_ACCOUNT_ID = "azure-sdk";
        private const string TARGET_PROJECT_ID = "29ec6040-b234-4e31-b139-33dc4287b756";
        private const int TARGET_DEFINITION_ID = 297;
        private const string DEVOPS_PATH = "https://dev.azure.com/azure-sdk";

        private readonly VssCredentials visualStudioCredentials;
        private readonly VssConnection visualStudioConnection;
        private readonly PipelineWitnessSettings testSettings = new()
        {
            PipelineOwnersDefinitionId = 5112,
            PipelineOwnersFilePath = "pipelineOwners/pipelineOwners.json",
            PipelineOwnersArtifactName = "pipelineOwners"
        };


        public BlobUploadProcessorIntegrationTests()
        {
            string pat = Environment.GetEnvironmentVariable("AZURESDK_DEVOPS_TOKEN");
            string blobUri = Environment.GetEnvironmentVariable("AZURESDK_BLOB_CS");

            if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(blobUri))
            {
                this.visualStudioCredentials = new VssBasicCredential("nobody", pat);
                this.visualStudioConnection = new VssConnection(new Uri(DEVOPS_PATH), this.visualStudioCredentials);
            }
        }

        [EnvironmentConditionalSkipFact]
        public async Task BasicBlobProcessInvokesSuccessfully()
        {
            BlobServiceClient blobServiceClient = new(Environment.GetEnvironmentVariable("AZURESDK_BLOB_CS"));
            BuildHttpClient buildHttpClient = this.visualStudioConnection.GetClient<BuildHttpClient>();

            List<Build> recentBuilds = await buildHttpClient.GetBuildsAsync(TARGET_PROJECT_ID, definitions: new[] { TARGET_DEFINITION_ID }, resultFilter: BuildResult.Succeeded, statusFilter: BuildStatus.Completed, top: 1, queryOrder: BuildQueryOrder.FinishTimeDescending);
            Assert.True(recentBuilds.Count > 0);
            int targetBuildId = recentBuilds.First().Id;

            AzurePipelinesProcessor processor = new(logger: new NullLogger<AzurePipelinesProcessor>(),
                blobServiceClient: blobServiceClient,
                vssConnection: this.visualStudioConnection,
                options: Options.Create<PipelineWitnessSettings>(this.testSettings));

            await processor.UploadBuildBlobsAsync(TARGET_ACCOUNT_ID, new Guid(TARGET_PROJECT_ID), targetBuildId);
        }

        [Theory]
        [InlineData(52438, 10000, 6)]
        [InlineData(10000, 10000, 1)]
        [InlineData(5200, 10000, 1)]
        [InlineData(0, 10000, 0)]
        public void TestBatching(int startingNumber, int batchSize, int expectedBatchNumber)
        {
            int numberOfBatches = AzurePipelinesProcessor.CalculateBatches(startingNumber, batchSize);

            Assert.Equal(expectedBatchNumber, numberOfBatches);
        }
    }
}
