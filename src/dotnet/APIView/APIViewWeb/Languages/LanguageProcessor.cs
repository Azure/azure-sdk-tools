using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using Microsoft.ApplicationInsights;

namespace APIViewWeb
{
    public abstract class LanguageProcessor : LanguageService
    {
        private readonly TelemetryClient _telemetryClient;

        public LanguageProcessor(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient;
        }

        public abstract string ProcessName { get; }
        public abstract string GetProcessorArguments(string originalName, string tempDirectory, string jsonPath);

        public override bool CanUpdate(string versionString)
        {
            return versionString != VersionString;
        }

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis, string crossLanguageMetadata = null)
        {
            var tempPath = Path.GetTempPath();
            var randomSegment = Guid.NewGuid().ToString("N");
            var tempDirectory = Path.Combine(tempPath, "ApiView", randomSegment);
            Directory.CreateDirectory(tempDirectory);
            originalName = Path.GetFileName(originalName);
            // Replace spaces and parentheses in the file name to remove invalid file name in cosmos DB.
            // temporary work around. We need to make sure FileName is set for all requests.
            originalName = originalName.Replace(" ", "_").Replace("(", "").Replace(")", "");
            var originalFilePath = Path.Combine(tempDirectory, originalName);

            var jsonFilePath = Path.ChangeExtension(originalFilePath, ".json");

            using (var file = File.Create(originalFilePath))
            {
                await stream.CopyToAsync(file);
            }
            var arguments = GetProcessorArguments(originalName, tempDirectory, jsonFilePath);
            return await RunParserProcess(originalName, tempDirectory, jsonFilePath, arguments);
        }

        public string RunProcess(string workingDirectory, string processName, string arguments)
        {
            var processStartInfo = new ProcessStartInfo(processName, arguments);
            string processErrors = String.Empty;

            processStartInfo.WorkingDirectory = workingDirectory;
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.CreateNoWindow = true;

            var output = new StringBuilder();
            var error = new StringBuilder();

            using (var process = new Process())
            {
                process.StartInfo = processStartInfo;
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                        output.AppendLine(args.Data);
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                        error.AppendLine(args.Data);
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit(90000);

                if (process.ExitCode != 0)
                {
                    processErrors = "Processor failed: " + Environment.NewLine +
                        "stdout: " + Environment.NewLine +
                        output + Environment.NewLine +
                        "stderr: " + Environment.NewLine +
                        error + Environment.NewLine;
                    throw new InvalidOperationException(processErrors);
                }
            }
            return processErrors;
        }

        public async Task<CodeFile> RunParserProcess(string originalName, string tempDirectory, string jsonPath, string arguments)
        {
            try
            {
                var processErrors = RunProcess(tempDirectory, ProcessName, arguments);

                _telemetryClient.TrackEvent($"Completed {Name} process run to parse " + originalName);

                if (File.Exists(jsonPath))
                {
                    using (var codeFileStream = new FileStream(jsonPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        CodeFile codeFile = await CodeFile.DeserializeAsync(stream: codeFileStream);
                        codeFile.VersionString = VersionString;
                        codeFile.Language = Name;
                        return codeFile;
                    }
                }
                else
                {
                    _telemetryClient.TrackTrace($"Processor failed to generate json file {jsonPath} with error {processErrors}");
                    throw new InvalidOperationException($"Processor failed to generate json file {jsonPath}");
                }
            }
            finally
            {
                await Task.Delay(1000);
                try
                {
                    Directory.Delete(tempDirectory, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete directory: {ex.Message}");
                }
            }
        }
    }
}
