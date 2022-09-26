// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class BlobCodeFileRepository
    {
        private BlobContainerClient _container;
        private readonly IMemoryCache _cache;

        public BlobCodeFileRepository(IConfiguration configuration, IMemoryCache cache, BlobContainerClient blobContainerClient = null)
        {
            _container = blobContainerClient ?? new BlobContainerClient(configuration["Blob:ConnectionString"], "codefiles");
            _cache = cache;
        }


        public Task<RenderedCodeFile> GetCodeFileAsync(ReviewRevisionModel revision, bool updateCache = true)
        {
            return GetCodeFileAsync(revision.RevisionId, revision.SingleFile.ReviewFileId, updateCache);
        }

        public async Task<RenderedCodeFile> GetCodeFileAsync(string revisionId, string codeFileId, bool updateCache = true)
        {
            var client = GetBlobClient(revisionId, codeFileId, out var key);

            if (_cache.TryGetValue<RenderedCodeFile>(key, out var codeFile))
            {
                return codeFile;
            }

            var info = await client.DownloadAsync();
            codeFile = new RenderedCodeFile(await CodeFile.DeserializeAsync(info.Value.Content));

            if (updateCache)
            {
                using var _ = _cache.CreateEntry(key)
                .SetSlidingExpiration(TimeSpan.FromHours(2))
                .SetValue(codeFile);
            }            

            return codeFile;
        }

        private BlobClient GetBlobClient(string revisionId, string codeFileId, out string key)
        {
            if (revisionId == codeFileId)
            {
                key = codeFileId;
            }
            else
            {
                key = revisionId + "/" + codeFileId;
            }
            return _container.GetBlobClient(key);
        }

        public async Task UpsertCodeFileAsync(string revisionId, string codeFileId, CodeFile codeFile)
        {
            var memoryStream = new MemoryStream();
            await codeFile.SerializeAsync(memoryStream);
            memoryStream.Position = 0;
            await GetBlobClient(revisionId, codeFileId, out var key).UploadAsync(memoryStream, overwrite: true);
            _cache.Remove(key);

            var renderedCodeFile = new RenderedCodeFile(codeFile);
            _cache.CreateEntry(key)
            .SetSlidingExpiration(TimeSpan.FromMinutes(10))
            .SetValue(renderedCodeFile);
        }

        public async Task DeleteCodeFileAsync(string revisionId, string codeFileId)
        {
            await GetBlobClient(revisionId, codeFileId, out var key).DeleteAsync();
            _cache.Remove(key);
        }
    }
}
