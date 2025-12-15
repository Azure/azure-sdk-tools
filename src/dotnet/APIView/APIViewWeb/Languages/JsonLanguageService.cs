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
        public override string VersionString { get; } = "1.0";
        public override bool UsesTreeStyleParser { get; } = false;

        public override bool CanUpdate(string versionString) => false;

        public override bool IsSupportedFile(string name)
        {
            // Skip JS uploads
            return base.IsSupportedFile(name) && !name.EndsWith(".api.json", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".rust.json", StringComparison.OrdinalIgnoreCase);
        }

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis, string croscrossLanguageMetada = null)
        {
            return await CodeFile.DeserializeAsync(stream);
        }
    }
}
