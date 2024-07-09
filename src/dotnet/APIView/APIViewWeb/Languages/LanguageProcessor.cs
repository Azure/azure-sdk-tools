using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Helpers;
using Microsoft.ApplicationInsights;
using Microsoft.IdentityModel.Abstractions;
using Octokit;

namespace APIViewWeb
{
    public abstract class LanguageProcessor: LanguageService
    {
        public abstract string ProcessName { get; }
        public abstract string VersionString { get; }
        public abstract string GetProcessorArguments(string originalName, string tempDirectory, string jsonPath);

        public override bool CanUpdate(string versionString)
        {
            return versionString != VersionString;
        }

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis, TelemetryClient telemetryClient = null)
        {
            var tempPath = Path.GetTempPath();
            var randomSegment = Guid.NewGuid().ToString("N");
            var tempDirectory = Path.Combine(tempPath, "ApiView", randomSegment);
            Directory.CreateDirectory(tempDirectory);
            var originalFilePath = Path.Combine(tempDirectory, originalName);
            if (telemetryClient != null) {
                telemetryClient.TrackTrace("OriginalFilePath: " + originalFilePath);
            }

            var jsonFilePath = (LanguageServiceHelpers.UseTreeStyleParser(this.Name)) ? Path.ChangeExtension(originalFilePath, ".json.tgz") : Path.ChangeExtension(originalFilePath, ".json");
            if (telemetryClient != null) {
                telemetryClient.TrackTrace("JsonFilePath: " + jsonFilePath);
            }


            using (var file = File.Create(originalFilePath))
            {
                await stream.CopyToAsync(file);
            }

            try
            {
                var arguments = GetProcessorArguments(originalName, tempDirectory, jsonFilePath);
                if (telemetryClient != null)
                {
                    telemetryClient.TrackTrace("ProcessorArguments: " + arguments);
                }
                var processStartInfo = new ProcessStartInfo(ProcessName, arguments);
                processStartInfo.WorkingDirectory = tempDirectory;
                processStartInfo.RedirectStandardError = true;
                processStartInfo.RedirectStandardOutput = true;

                using (var process = Process.Start(processStartInfo))
                {
                    process.WaitForExit();
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

                using (var codeFileStream = File.OpenRead(jsonFilePath))
                {
                    CodeFile codeFile = await CodeFile.DeserializeAsync(stream: codeFileStream, doTreeStyleParserDeserialization: LanguageServiceHelpers.UseTreeStyleParser(this.Name));
                    codeFile.VersionString = VersionString;
                    codeFile.Language = Name;
                    return codeFile;
                }
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }
}
