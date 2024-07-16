// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class BlobCodeFileRepository : IBlobCodeFileRepository
    {
        private readonly IMemoryCache _cache;
        private BlobServiceClient _serviceClient;

        public BlobCodeFileRepository(IConfiguration configuration, IMemoryCache cache)
        {
            _serviceClient = new BlobServiceClient(new Uri(configuration["StorageAccountUrl"]), new DefaultAzureCredential());
            _cache = cache;
        }


        public Task<RenderedCodeFile> GetCodeFileAsync(APIRevisionListItemModel revision, bool updateCache = true)
        {
            return GetCodeFileAsync(revision.Id, revision.Files.Single().FileId, revision.Language, updateCache);
        }

        public async Task<RenderedCodeFile> GetCodeFileAsync(string revisionId, string codeFileId, string language, bool updateCache = true)
        {
            var client = GetBlobClient(revisionId, codeFileId, out var key);

            if (_cache.TryGetValue<RenderedCodeFile>(key, out var codeFile))
            {
                return codeFile;
            }

            var info = await client.DownloadAsync();

            CodeFile deserializedCodeFile = null;
            // Try to deserialize the code file twice, as the first time might fail due to the file being not yet updated to new tree token format.
            // This is a temporary work around. We should have a property in Cosmos revision to indicate whether a token is using new format or old format.
            try
            {
                deserializedCodeFile = await CodeFile.DeserializeAsync(info.Value.Content, doTreeStyleParserDeserialization: LanguageServiceHelpers.UseTreeStyleParser(language));
            }
            catch
            {
                deserializedCodeFile = await CodeFile.DeserializeAsync(info.Value.Content, doTreeStyleParserDeserialization: false);
            }
            codeFile = new RenderedCodeFile(deserializedCodeFile);

            if (updateCache)
            {
                using var _ = _cache.CreateEntry(key)
                .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                .SetValue(codeFile);
            }            

            return codeFile;
        }

        public async Task<CodeFile> GetCodeFileWithCompressionAsync(string revisionId, string codeFileId, bool updateCache = true)
        {
            var client = GetBlobClient(revisionId, codeFileId, out var key);

            if (_cache.TryGetValue<CodeFile>(key, out var codeFile))
            {
                return codeFile;
            }
            var info = await client.DownloadAsync();
            codeFile = await CodeFile.DeserializeAsync(info.Value.Content, doTreeStyleParserDeserialization: true);
            if (updateCache)
            {
                using var _ = _cache.CreateEntry(key)
                    .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                    .SetValue(codeFile);
            }
            return codeFile;
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
