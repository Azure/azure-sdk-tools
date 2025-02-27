// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Caching.Memory;
using Azure;
using Microsoft.Extensions.Logging;

namespace APIViewWeb
{
    public class BlobCodeFileRepository : IBlobCodeFileRepository
    {
        private readonly IMemoryCache _cache;
        private readonly BlobServiceClient _serviceClient;
        private readonly ILogger<BlobCodeFileRepository> _logger;

        public BlobCodeFileRepository(BlobServiceClient blobServiceClient, IMemoryCache cache, ILogger<BlobCodeFileRepository> logger)
        {
            _serviceClient = blobServiceClient;
            _cache = cache;
            _logger = logger;
        }

        public Task<RenderedCodeFile> GetCodeFileAsync(APIRevisionListItemModel revision, bool updateCache = true)
        {
            return GetCodeFileAsync(revision.Id, revision.Files.Single(), revision.Language, updateCache);
        }

        public async Task<RenderedCodeFile> GetCodeFileAsync(string revisionId, APICodeFileModel apiCodeFile, string language, bool updateCache = true)
        {
            var client = GetBlobClient(revisionId, apiCodeFile.FileId, out var key);
            if (_cache.TryGetValue<RenderedCodeFile>(key, out var codeFile))
            {
                return codeFile;
            }

            try
            {
                var info = await client.DownloadAsync();
                codeFile = new RenderedCodeFile(await CodeFile.DeserializeAsync(info.Value.Content));
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Error retrieving code file with revision ID {RevisionId} and file ID {FileId}", revisionId, apiCodeFile.FileId);
                throw;
            }

            if (updateCache)
            {
                using var _ = _cache.CreateEntry(key)
                .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                .SetValue(codeFile);
            }
            return codeFile;
        }

        public async Task<CodeFile> GetCodeFileFromStorageAsync(string revisionId, string codeFileId)
        {
            var client = GetBlobClient(revisionId, codeFileId, out var key);
            try
            {
                var info = await client.DownloadAsync();
                var codeFile = await CodeFile.DeserializeAsync(info.Value.Content);
                return codeFile;
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Error retrieving code file from storage with revision ID {RevisionId} and file ID {FileId}", revisionId, codeFileId);
                throw;
            }
        }

        public async Task UpsertCodeFileAsync(string revisionId, string codeFileId, CodeFile codeFile)
        {
            var memoryStream = new MemoryStream();
            await codeFile.SerializeAsync(memoryStream);
            memoryStream.Position = 0;
            try
            {
                await GetBlobClient(revisionId, codeFileId, out var key).UploadAsync(memoryStream, overwrite: true);
                _cache.Remove(key);

                var renderedCodeFile = new RenderedCodeFile(codeFile);
                _cache.CreateEntry(key)
                .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                .SetValue(renderedCodeFile);
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Error upserting code file with revision ID {RevisionId} and file ID {FileId}", revisionId, codeFileId);
                throw;
            }
        }

        public async Task DeleteCodeFileAsync(string revisionId, string codeFileId)
        {
            try
            {
                await GetBlobClient(revisionId, codeFileId, out var key).DeleteAsync();
                _cache.Remove(key);
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Error deleting code file with revision ID {RevisionId} and file ID {FileId}", revisionId, codeFileId);
                throw;
            }
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
            var container = _serviceClient.GetBlobContainerClient("codefiles");
            return container.GetBlobClient(key);
        }
    }
}
