// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class BlobOriginalsRepository
    {
        private BlobContainerClient _container;

        public BlobOriginalsRepository(IConfiguration configuration)
        {
            var connectionString = configuration["Blob:ConnectionString"];
            _container = new BlobContainerClient(connectionString, "originals");
        }

        public async Task<Stream> GetOriginalAsync(string codeFileId)
        {
            var info = await GetBlobClient(codeFileId).DownloadAsync();
            return info.Value.Content;
        }

        private BlobClient GetBlobClient(string codeFileId)
        {
            return _container.GetBlobClient(codeFileId);
        }

        public async Task UploadOriginalAsync(string codeFileId, Stream stream)
        {
            await GetBlobClient(codeFileId).UploadAsync(stream);
        }

        public async Task DeleteOriginalAsync(string codeFileId)
        {
            await GetBlobClient(codeFileId).DeleteAsync();
        }
    }
}