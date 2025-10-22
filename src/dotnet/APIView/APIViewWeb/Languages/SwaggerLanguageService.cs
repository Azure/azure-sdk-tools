// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using ApiView;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class SwaggerLanguageService : LanguageProcessor
    {
        private readonly string _reviewGenerationPipelineUrl;
        public override string Name { get; } = "Swagger";

        public override string[] Extensions { get; } = { ".swagger" };

        public override string VersionString { get; } = "0";

        public override string ProcessName => throw new NotImplementedException();

        public override bool UsesTreeStyleParser { get; } = false;
        public override string ReviewGenerationPipelineUrl => _reviewGenerationPipelineUrl;

        public SwaggerLanguageService(IConfiguration configuration, TelemetryClient telemetryClient) : base(telemetryClient)
        {
            IsReviewGenByPipeline = true;
            _reviewGenerationPipelineUrl = configuration["SwaggerReviewGenerationPipelineUrl"] ?? String.Empty;
        }
        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis, string crossLanguageMetadata = null)
        {
            return await CodeFile.DeserializeAsync(stream, hasSections: true);
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
