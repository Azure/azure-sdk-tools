// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using APIViewWeb.Repositories;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure;
using Microsoft.Extensions.Logging;

namespace APIViewWeb
{
    public class BlobOriginalsRepository : IBlobOriginalsRepository
    {
        private readonly BlobContainerClient _container;
        private readonly ILogger<BlobOriginalsRepository> _logger;

        public BlobOriginalsRepository(BlobServiceClient blobServiceClient, ILogger<BlobOriginalsRepository> logger)
        {
            _container = blobServiceClient.GetBlobContainerClient("originals");
            _logger = logger;
        }

        public string GetContainerUrl() => _container.Uri.ToString();

        public async Task<Stream> GetOriginalAsync(string codeFileId)
        {
            try
            {
                var info = await GetBlobClient(codeFileId).DownloadAsync();
                return info.Value.Content;
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Error retrieving original with ID {CodeFileId}", codeFileId);
                throw;
            }
        }

        public async Task UploadOriginalAsync(string codeFileId, Stream stream)
        {
            try
            {
                await GetBlobClient(codeFileId).UploadAsync(stream);
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Error uploading original with ID {CodeFileId}", codeFileId);
                throw;
            }
        }

        public async Task DeleteOriginalAsync(string codeFileId)
        {
            try
            {
                await GetBlobClient(codeFileId).DeleteAsync();
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Error deleting original with ID {CodeFileId}", codeFileId);
                throw;
            }
        }

        private BlobClient GetBlobClient(string codeFileId)
        {
            return _container.GetBlobClient(codeFileId);
        }
    }
}
