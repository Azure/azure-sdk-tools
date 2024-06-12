// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public class JsonLanguageService : LanguageService
    {
        public override string Name { get; } = "Json";
        public override string[] Extensions { get; } = { ".json", ".json.tgz" };

        public override bool CanUpdate(string versionString) => false;

        public override bool IsSupportedFile(string name)
        {
            // Skip JS uploads
            return base.IsSupportedFile(name) && !name.EndsWith(".api.json", StringComparison.OrdinalIgnoreCase);
        }

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            if (originalName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                using (GZipStream gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true))
                {
                    return await CodeFile.DeserializeAsync(gzipStream, useTreeStyleParserDeserializerOptions: true);
                }
            }
            return await CodeFile.DeserializeAsync(stream, true);
        }
    }
}
