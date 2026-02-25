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
    public class PythonLanguageService : LanguageProcessor
    {
        private readonly string _pythonExecutablePath;
        private readonly string _reviewGenerationPipelineUrl;
        private readonly TelemetryClient _telemetryClient;
        public override string Name { get; } = "Python";
        public override string[] Extensions { get; } = { ".whl" };
        public override string VersionString { get; } = "0.3.26";
        public override string ProcessName => _pythonExecutablePath;
        public override string ReviewGenerationPipelineUrl => _reviewGenerationPipelineUrl;

        public PythonLanguageService(IConfiguration configuration, TelemetryClient telemetryClient) : base(telemetryClient)
        {
            _pythonExecutablePath = configuration["PYTHONEXECUTABLEPATH"] ?? "python";
            _reviewGenerationPipelineUrl = configuration["PythonReviewGenerationPipelineUrl"] ?? String.Empty;
            _telemetryClient = telemetryClient;

            // Check if sandboxing is disabled for python
            bool.TryParse(configuration["ReviewGenByPipelineDisabledForPython"], out bool _isDisabledForPython);
            // Enable sandboxing when it's not disabled for python
            IsReviewGenByPipeline = ! _isDisabledForPython;
        }
        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            return $" -m apistub --pkg-path {originalName} --temp-path {tempDirectory}" +
                $" --out-path {jsonPath}";
        }

        private string GetPythonVirtualEnv(string tempDirectory)
        {
            // Create virtual instance
            RunProcess(tempDirectory, ProcessName, $" -m virtualenv {tempDirectory} --system-site-packages --seeder app-data --symlink-app-data");
            return Path.Combine(tempDirectory, "Scripts", "python.exe");
        }

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis, string crossLanguageMetadata = null)
        {
            var tempPath = Path.GetTempPath();
            _telemetryClient.TrackEvent("Creating code file for " + originalName);
            var randomSegment = Guid.NewGuid().ToString("N");
            var tempDirectory = Path.Combine(tempPath, "ApiView", randomSegment);
            Directory.CreateDirectory(tempDirectory);
            var originalFilePath = Path.Combine(tempDirectory, originalName);
            var jsonFilePath = Path.ChangeExtension(originalFilePath, ".json");

            using (var file = File.Create(originalFilePath))
            {
                await stream.CopyToAsync(file);
            }

            string mappingFilePath = null;
            if (!string.IsNullOrEmpty(crossLanguageMetadata))
            {
                mappingFilePath = Path.Combine(tempDirectory, "apiview-properties.json");
                await File.WriteAllTextAsync(mappingFilePath, crossLanguageMetadata);
            }

            string pythonVenvPath = GetPythonVirtualEnv(tempDirectory);
            string arguments = GetProcessorArgumentsWithMapping(originalName, tempDirectory, jsonFilePath, mappingFilePath);
            return await RunParserProcess(originalName, pythonVenvPath, jsonFilePath, arguments);
        }

        public string GetProcessorArgumentsWithMapping(string originalName, string tempDirectory, string jsonPath, string mappingFilePath)
        {
            string baseArgs = $" -m apistub --pkg-path {originalName} --temp-path {tempDirectory} --out-path {jsonPath}";

            if (!string.IsNullOrEmpty(mappingFilePath) && File.Exists(mappingFilePath))
            {
                baseArgs += $" --mapping-path {mappingFilePath}";
            }

            return baseArgs;
        }
    }
}
