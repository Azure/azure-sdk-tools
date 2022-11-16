// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using APIViewWeb.Repositories;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class BlobOriginalsRepository : IBlobOriginalsRepository
    {
        private BlobContainerClient _container;

        public string GetContainerUrl() => _container.Uri.ToString();

        public BlobOriginalsRepository(IConfiguration configuration)
        {
            _container = new BlobContainerClient(configuration["Blob:ConnectionString"], "originals");
        }

        public async Task<Stream> GetOriginalAsync(string codeFileId)
        {
            var info = await GetBlobClient(codeFileId).DownloadAsync();
            return info.Value.Content;
        }

        public async Task UploadOriginalAsync(string codeFileId, Stream stream)
        {
            await GetBlobClient(codeFileId).UploadAsync(stream);
        }

        public async Task DeleteOriginalAsync(string codeFileId)
        {
            await GetBlobClient(codeFileId).DeleteAsync();
        }

        private BlobClient GetBlobClient(string codeFileId)
        {
            return _container.GetBlobClient(codeFileId);
        }
    }
}
