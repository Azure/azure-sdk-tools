// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class PythonLanguageService : LanguageProcessor
    {
        private readonly string _pythonExecutablePath;
        private readonly TelemetryClient _telemetryClient;
        public override string Name { get; } = "Python";
        public override string[] Extensions { get; } = { ".whl" };
        public override string VersionString { get; } = "0.3.18";
        public override string ProcessName => _pythonExecutablePath;

        public PythonLanguageService(IConfiguration configuration, TelemetryClient telemetryClient) : base(telemetryClient)
        {
            _pythonExecutablePath = configuration["PYTHONEXECUTABLEPATH"] ?? "python";
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

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
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
            var pythonVenvPath = GetPythonVirtualEnv(tempDirectory);
            var arguments = GetProcessorArguments(originalName, tempDirectory, jsonFilePath);
            return await RunParserProcess(originalName, pythonVenvPath, jsonFilePath, arguments);
        }
    }
}
