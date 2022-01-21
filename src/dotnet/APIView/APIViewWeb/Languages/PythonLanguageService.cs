// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ApiView;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace APIViewWeb
{
    public class PythonLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "Python";
        public override string Extension { get; } = ".whl";
        public override string VersionString { get; } = "0.2.8";

        private readonly string _pythonExecutablePath;
        public override string ProcessName => _pythonExecutablePath;
        private readonly string _apiScriptPath;

        static TelemetryClient _telemetryClient = new TelemetryClient(TelemetryConfiguration.CreateDefault());

        public PythonLanguageService(IConfiguration configuration)
        {
            _pythonExecutablePath = configuration["PYTHONEXECUTABLEPATH"] ?? "python";
            _apiScriptPath = Path.Combine(Path.GetDirectoryName(typeof(PythonLanguageService).Assembly.Location), "api-stub-generator", "apistubgen.py");
        }
        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            return $"{_apiScriptPath} --pkg-path {originalName} --temp-path {tempDirectory}" +
                $" --out-path {jsonPath} --hide-report";
        }

        private string GetPythonVirtualEnv(string tempDirectory)
        {
            // Create virtual instance
            RunProcess(tempDirectory, ProcessName, $" -m virtualenv {tempDirectory} --system-site-packages");
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
            try
            {
                var apiStubGenPath = GetPythonVirtualEnv(tempDirectory);
                var arguments = GetProcessorArguments(originalName, tempDirectory, jsonFilePath);
                RunProcess(tempDirectory, apiStubGenPath, arguments);
                _telemetryClient.TrackEvent("Completed Python process run to parse " + originalName);
                using (var codeFileStream = File.OpenRead(jsonFilePath))
                {
                    var codeFile = await CodeFile.DeserializeAsync(codeFileStream);
                    codeFile.VersionString = VersionString;
                    codeFile.Language = Name;
                    return codeFile;
                }
            }
            catch(Exception ex)
            {
                _telemetryClient.TrackException(ex);
                throw ex;
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        private void RunProcess(string workingDirectory, string processName, string args)
        {
            var processStartInfo = new ProcessStartInfo(processName, args)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using (var process = Process.Start(processStartInfo))
            {
                process.WaitForExit(3 * 60 * 1000);
                _telemetryClient.TrackEvent("Completed parsing python wheel. Exit code: " + process.ExitCode);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "Processor failed: " + Environment.NewLine +
                        "stdout: " + Environment.NewLine +
                        process.StandardOutput.ReadToEnd() + Environment.NewLine +
                        "stderr: " + Environment.NewLine +
                        process.StandardError.ReadToEnd() + Environment.NewLine);
                }
            }
        }
    }
}
