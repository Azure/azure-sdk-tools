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
        public override string VersionString { get; } = "1.0.8";

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            var (tempDirectory, originalFilePath, jsonFilePath) = await PrepareTemporaryDirectory(originalName, stream);
            if (originalName.EndsWith(".zip"))
            {
                ZipFile.ExtractToDirectory(originalFilePath, tempDirectory);
            }

            try
            {
                return await ExecuteProcessorAsync(originalName, tempDirectory, jsonFilePath);
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
            if (originalName.EndsWith(".zip"))
            {
                return $"{jsPath} \"{tempDirectory}\" \"{jsonFilePath}\"";
            } else
            {
                return $"{jsPath} \"{originalName}\" \"{jsonFilePath}\"";
            }


        }
    }
}
