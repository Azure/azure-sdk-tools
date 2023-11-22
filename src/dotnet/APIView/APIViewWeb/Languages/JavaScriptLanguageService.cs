// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public class JavaScriptLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "JavaScript";
        public override string[] Extensions { get; } = { ".api.json", ".zip" };
        public override string ProcessName { get; } = "node";
        public override string VersionString { get; } = "1.0.7";

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            var tempPath = Path.GetTempPath();
            var randomSegment = Guid.NewGuid().ToString("N");
            var tempDirectory = Path.Combine(tempPath, "ApiView", randomSegment);
            Directory.CreateDirectory(tempDirectory);
            var originalFilePath = Path.Combine(tempDirectory, originalName);

            var jsonFilePath = Path.ChangeExtension(originalFilePath, ".json");

            using (var file = File.Create(originalFilePath))
            {
                await stream.CopyToAsync(file);
            }

            if (originalName.EndsWith(".zip"))
            {
                ZipFile.ExtractToDirectory(originalFilePath, tempDirectory);
            }

            try
            {
                var arguments = GetProcessorArguments(originalName, tempDirectory, jsonFilePath);
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
                    var codeFile = await CodeFile.DeserializeAsync(codeFileStream);
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

        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonFilePath)
        {
            var jsPath = Path.Combine(
      Path.GetDirectoryName(typeof(JavaScriptLanguageService).Assembly.Location),
      "export.js");

            var apiJsonFiles = Directory.EnumerateFiles(tempDirectory, "*.api.json");
            if (apiJsonFiles.Count() > 1)
            {
                return $"{jsPath} \"{tempDirectory}\" \"{jsonFilePath}\"";
            } else
            {
                return $"{jsPath} \"{apiJsonFiles.Single()}\" \"{jsonFilePath}\"";
            }


        }
    }
}
