// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public class CSharpLanguageService : ILanguageService
    {
        public string Name { get; } = "C#";

        public bool IsSupportedExtension(string extension)
        {
            return string.Equals(extension, ".dll", comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        public bool CanUpdate(string versionString)
        {
            return versionString != CodeFileBuilder.CurrentVersion;
        }

        public Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            return Task.FromResult(CodeFileBuilder.Build(stream, runAnalysis));
        }
    }
}