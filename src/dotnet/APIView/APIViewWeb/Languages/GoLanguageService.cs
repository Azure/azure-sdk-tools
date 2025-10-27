// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Threading.Tasks;
using ApiView;
using Microsoft.ApplicationInsights;
using System.Text;

namespace APIViewWeb
{
    public class GoLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "Go";
        public override string [] Extensions { get; } = { ".gosource" };
        public override string ProcessName { get; } = "apiviewgo";
        public override string VersionString { get; } = "0.1";

        public GoLanguageService(TelemetryClient telemetryClient) : base(telemetryClient)
        {
        }

        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonFilePath)
        {
            return $"\"{originalName}\" \"{jsonFilePath}\"";
        }

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis, string crossLanguageMetadata = null)
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

            var arguments = GetProcessorArguments(packageRootDirectory, tempDirectory, tempDirectory);
            return await RunParserProcess(packageRootDirectory, tempDirectory, jsonFilePath, arguments);
        }
    }
}
