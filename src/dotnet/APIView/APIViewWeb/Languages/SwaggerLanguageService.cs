// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using APIView;
using APIViewWeb.Models;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class SwaggerLanguageService : LanguageProcessor
    {
        private static readonly string[] WindowsReservedNames = {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

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

        public override bool GeneratePipelineRunParams(APIRevisionGenerationPipelineParamModel param)
        {
            if (param == null || string.IsNullOrWhiteSpace(param.FileName))
            {
                return false;
            }

            var safeFileName = Path.GetFileName(param.FileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                return false;
            }

            safeFileName = new string(safeFileName.Select(ch => Regex.IsMatch(ch.ToString(), "[A-Za-z0-9._-]") ? ch : '_').ToArray());

            safeFileName = safeFileName.TrimEnd('. ');

            if (string.IsNullOrEmpty(safeFileName) || safeFileName == "." || safeFileName == "..")
            {
                return false;
            }

            var nameWithoutExt = Path.GetFileNameWithoutExtension(safeFileName).ToUpperInvariant();
            if (WindowsReservedNames.Contains(nameWithoutExt))
            {
                return false;
            }

            param.FileName = safeFileName;
            return true;
        }
    }
}
