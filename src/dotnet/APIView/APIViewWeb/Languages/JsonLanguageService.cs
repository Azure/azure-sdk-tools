// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public class JsonLanguageService : LanguageService
    {
        public override string Name { get; } = "Json";
        public override string[] Extensions { get; } = { ".json" };

        public override bool CanUpdate(string versionString) => false;

        public override bool IsSupportedFile(string name)
        {
            // Skip JS uploads
            return base.IsSupportedFile(name) && !name.EndsWith(".api.json", StringComparison.OrdinalIgnoreCase);
        }

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            return await CodeFile.DeserializeAsync(stream, true);
        }
    }
}
