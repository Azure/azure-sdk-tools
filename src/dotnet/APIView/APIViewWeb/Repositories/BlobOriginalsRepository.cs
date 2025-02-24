// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using APIViewWeb.Repositories;
using Azure.Identity;
using Azure.Storage.Blobs;

namespace APIViewWeb
{
    public class BlobOriginalsRepository : IBlobOriginalsRepository
    {
        private BlobContainerClient _container;

        public string GetContainerUrl() => _container.Uri.ToString();

        public BlobOriginalsRepository(BlobServiceClient blobServiceClient)
        {
            _container = blobServiceClient.GetBlobContainerClient("originals");
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
