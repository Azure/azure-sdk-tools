// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public class CSharpLanguageService : LanguageService
    {
        public override string Name { get; } = "C#";

        public override string Extension { get; } = ".dll";

        public override bool CanUpdate(string versionString)
        {
            return versionString != CodeFileBuilder.CurrentVersion;
        }

        public override Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            return Task.FromResult(CodeFileBuilder.Build(stream, runAnalysis));
        }
    }
}