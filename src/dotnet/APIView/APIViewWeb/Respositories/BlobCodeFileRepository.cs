// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading.Tasks;
using ApiView;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class BlobCodeFileRepository
    {
        private BlobContainerClient _container;

        public BlobCodeFileRepository(IConfiguration configuration)
        {
            var connectionString = configuration["Blob:ConnectionString"];
            _container = new BlobContainerClient(connectionString, "codefiles");
        }

        public async Task<CodeFile> GetCodeFileAsync(string codeFileId)
        {
            var info = await GetBlobClient(codeFileId).DownloadAsync();
            return await CodeFile.DeserializeAsync(info.Value.Content);
        }

        private BlobClient GetBlobClient(string codeFileId)
        {
            return _container.GetBlobClient(codeFileId);
        }

        public async Task UpsertCodeFileAsync(string codeFileId, CodeFile codeFile)
        {
            var memoryStream = new MemoryStream();
            await codeFile.SerializeAsync(memoryStream);
            memoryStream.Position = 0;
            await GetBlobClient(codeFileId).UploadAsync(memoryStream);
        }

        public async Task DeleteCodeFileAsync(string codeFileId)
        {
            await GetBlobClient(codeFileId).DeleteAsync();
        }
    }
}