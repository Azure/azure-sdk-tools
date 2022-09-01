// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System.Text;
using Microsoft.VisualStudio.Services.FileContainer;
using Octokit;
using System;

namespace APIViewWeb
{
    public class BlobUsageSampleRepository
    {
        private BlobContainerClient _container;

        public BlobUsageSampleRepository(IConfiguration configuration)
        {
            var connectionString = configuration["Blob:ConnectionString"];
            _container = new BlobContainerClient(connectionString, "usagesamples"); 
        }

        public async Task<Stream> GetUsageSampleAsync(string sampleFileId)
        {
            try
            {
                var info = await GetBlobClient(sampleFileId).DownloadAsync();
                return info.Value.Content;
            }
            catch (Exception e)
            {
                // Error handling- Allows tidy pages to be displayed for a blob not existing
                if(e.Message.StartsWith("The specified container does not exist."))
                {
                    return new MemoryStream(Encoding.UTF8.GetBytes("Bad Blob"));
                }
                else
                {
                    return null;
                }
            }
        }

        private BlobClient GetBlobClient(string sampleFileId)
        {
            return _container.GetBlobClient(sampleFileId);
        }

        public async Task UploadUsageSampleAsync(string sampleFileId, Stream stream)
        {
            await GetBlobClient(sampleFileId).UploadAsync(stream, overwrite:true);
        }

        public async Task DeleteUsageSampleAsync (string sampleFileId)
        {
            await GetBlobClient(sampleFileId).DeleteAsync();
        }
    }
}
