// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public class PythonLanguageService : ILanguageService
    {
        public string Name { get; } = "Python";

        public string pythonParserVersion = "0.1.1";

        public bool IsSupportedExtension(string extension)
        {
            return string.Equals(extension, ".whl", comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        public bool CanUpdate(string versionString)
        {
            return versionString != pythonParserVersion;
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
                var pythonScriptPath = Path.Combine(
                    Path.GetDirectoryName(typeof(PythonLanguageService).Assembly.Location),
                    "api-stub-generator",
                    "apistubgen.py"
                    );
                var arguments = $"{pythonScriptPath} --pkg-path {originalFilePath} --temp-path {tempDirectory}" +
                    $" --out-path {jsonFilePath} --hide-report";
                var processStartInfo = new ProcessStartInfo("python", arguments);
                processStartInfo.WorkingDirectory = tempDirectory;
                processStartInfo.RedirectStandardError = true;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.UseShellExecute = false;

                using (var process = Process.Start(processStartInfo))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException(
                            "Python stub generator failed: " + Environment.NewLine +
                            "stdout: " + Environment.NewLine +
                            process.StandardOutput.ReadToEnd() + Environment.NewLine +
                            "stderr: " + Environment.NewLine +
                            process.StandardError.ReadToEnd() + Environment.NewLine);
                    }
                }
                using (var codeFileStream = File.OpenRead(jsonFilePath))
                {
                    var codeFile = await CodeFile.DeserializeAsync(codeFileStream);
                    codeFile.VersionString = pythonParserVersion;
                    codeFile.Language = "Python";
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
