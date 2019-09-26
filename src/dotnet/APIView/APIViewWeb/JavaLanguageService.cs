// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public class JavaLanguageService : ILanguageService
    {
        public bool IsSupportedExtension(string extension)
        {
            return string.Equals(extension, ".json", comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        public bool CanUpdate(CodeFile codeFile)
        {
            return false;
        }

        public async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
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

            try
            {
                var arguments = $"-jar {JarName} \"{originalFilePath}\" \"{tempDirectory}\"";
                var processStartInfo = new ProcessStartInfo("java", arguments);
                processStartInfo.RedirectStandardError = true;
                processStartInfo.RedirectStandardOutput = true;

                using (var process = Process.Start(processStartInfo))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException(
                            "Java processor failed: " + Environment.NewLine +
                            "stdout: " + Environment.NewLine +
                            process.StandardOutput.ReadToEnd() + Environment.NewLine +
                            "stderr: " + Environment.NewLine +
                            process.StandardError.ReadToEnd() + Environment.NewLine);
                    }
                }

                using (var codeFileStream = File.OpenRead(jsonFilePath))
                {
                    var codeFile = await CodeFile.DeserializeAsync(codeFileStream);
                    codeFile.Version = JarName;
                    codeFile.Language = "Java";
                    return codeFile;
                }
            }
            finally
            {
                Directory.Delete(tempDirectory);
            }
        }

        public string JarName = "java-api-listing-1.0.0.jar";
    }
}