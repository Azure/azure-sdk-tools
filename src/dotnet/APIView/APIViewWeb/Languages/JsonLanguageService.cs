// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public class JsonLanguageService : ILanguageService
    {
        public string Name { get; } = "Json";

        public bool IsSupportedExtension(string extension)
        {
            return string.Equals(extension, ".json", comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        public bool CanUpdate(string codeFile)
        {
            return false;
        }

        public async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            return await CodeFile.DeserializeAsync(stream);
        }
    }
}