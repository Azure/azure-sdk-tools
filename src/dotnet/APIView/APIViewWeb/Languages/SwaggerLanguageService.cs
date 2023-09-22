// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public class SwaggerLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "Swagger";

        public override string[] Extensions { get; } = { ".swagger" };

        public override string VersionString { get; } = "0";

        public override string ProcessName => throw new NotImplementedException();

        public SwaggerLanguageService()
        {
            IsReviewGenByPipeline = true;
        }
        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            return await CodeFile.DeserializeAsync(stream, true);
        }

        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            throw new NotImplementedException();
        }
        public override bool CanUpdate(string versionString)
        {
            return false;
        }
    }
}
