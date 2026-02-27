// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class CSharpLanguageService : LanguageProcessor
    {
        private readonly string _csharpParserToolPath;
        public override string Name { get; } = "C#";
        public override string[] Extensions { get; } = { ".dll" };
        public override string ProcessName => _csharpParserToolPath;
        public override string VersionString { get; } = "29.91";

        public CSharpLanguageService(IConfiguration configuration, TelemetryClient telemetryClient) : base(telemetryClient)
        {
            _csharpParserToolPath = configuration["CSHARPPARSEREXECUTABLEPATH"];
        }

        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            var outputFileName = Path.GetFileName(jsonPath).Replace(".json", "");
            return $"--packageFilePath \"{originalName}\" --outputDirectoryPath \"{tempDirectory}\" --outputFileName \"{outputFileName}\"";
        }

        public override bool IsSupportedFile(string name)
        {
            return IsDll(name) || IsNuget(name);
        }

        private static bool IsDll(string name)
        {
            return name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNuget(string name)
        {
            return name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase);
        }

        public override bool CanUpdate(string versionString)
        {
            return versionString != VersionString;
        }
    }
}
