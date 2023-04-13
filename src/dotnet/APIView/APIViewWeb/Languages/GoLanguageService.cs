// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public class GoLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "Go";
        public override string [] Extensions { get; } = { ".gosource" };
        public override string ProcessName { get; } = "apiviewgo";
        public override string VersionString { get; } = "2";

        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonFilePath)
        {
            return $"\"{originalName}\" \"{jsonFilePath}\"";
        }

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            var tempPath = Path.GetTempPath();
            var randomSegment = Guid.NewGuid().ToString("N");
            var tempDirectory = Path.Combine(tempPath, "ApiView", randomSegment);
            Directory.CreateDirectory(tempDirectory);
            var originalFilePath = Path.Combine(tempDirectory, originalName);
            var jsonFilePath = Path.ChangeExtension(originalFilePath, ".json");

            MemoryStream zipStream = new MemoryStream();
            await stream.CopyToAsync(zipStream);
            zipStream.Position = 0;
            var archive = new ZipArchive(zipStream);            
            archive.ExtractToDirectory(tempDirectory);
            var packageRootDirectory = originalFilePath.Replace(Extensions[0], "");

            try
            {
                var arguments = GetProcessorArguments(packageRootDirectory, tempDirectory, tempDirectory);
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
    }
}
