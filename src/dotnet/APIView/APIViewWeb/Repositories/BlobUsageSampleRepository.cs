// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Azure;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using System.Text;
using Azure.Storage.Blobs.Models;

namespace APIViewWeb.Repositories
{
    public class BlobUsageSampleRepository : IBlobUsageSampleRepository
    {
        private readonly BlobServiceClient _serviceClient;
        private readonly ILogger<BlobUsageSampleRepository> _logger;

        public BlobUsageSampleRepository(BlobServiceClient blobServiceClient, ILogger<BlobUsageSampleRepository> logger)
        {
            _serviceClient = blobServiceClient;
            _logger = logger;
        }

        public async Task<Stream> GetUsageSampleAsync(string sampleFileId)
        {
            try
            {
                var info = await GetBlobClient(sampleFileId).DownloadAsync();
                return info.Value.Content;
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Error retrieving usage sample with ID {SampleFileId}", sampleFileId);
                if (e.ErrorCode == BlobErrorCode.ContainerNotFound.ToString())
                {
                    return new MemoryStream(Encoding.UTF8.GetBytes("Bad Blob"));
                }
                else
                {
                    throw;
                }
            }
        }

        public async Task UploadUsageSampleAsync(string sampleFileId, Stream stream)
        {
            try
            {
                await GetBlobClient(sampleFileId).UploadAsync(stream, overwrite: true);
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Error uploading usage sample with ID {SampleFileId}", sampleFileId);
                throw;
            }
        }

        public async Task DeleteUsageSampleAsync(string sampleFileId)
        {
            try
            {
                await GetBlobClient(sampleFileId).DeleteAsync();
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Error deleting usage sample with ID {SampleFileId}", sampleFileId);
                throw;
            }
        }

        private BlobClient GetBlobClient(string sampleFileId)
        {
            var container = _serviceClient.GetBlobContainerClient("usagesamples");
            return container.GetBlobClient(sampleFileId);
        }
    }
}
