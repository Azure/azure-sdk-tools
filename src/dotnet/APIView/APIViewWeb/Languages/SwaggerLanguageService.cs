// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Helpers;
using Microsoft.ApplicationInsights;

namespace APIViewWeb
{
    public class SwaggerLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "Swagger";

        public override string[] Extensions { get; } = { ".swagger" };

        public override string VersionString { get; } = "0";

        public override string ProcessName => throw new NotImplementedException();

        public SwaggerLanguageService(TelemetryClient telemetryClient) : base(telemetryClient)
        {
            IsReviewGenByPipeline = true;
        }
        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            return await CodeFile.DeserializeAsync(stream, hasSections: true, doTreeStyleParserDeserialization: LanguageServiceHelpers.UseTreeStyleParser(this.Name));
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
