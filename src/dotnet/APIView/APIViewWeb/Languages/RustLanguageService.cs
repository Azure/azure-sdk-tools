// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class RustLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "Rust";
        public override string[] Extensions { get; } = { ".rust.json" };
        public override string ProcessName { get; } = "node";
        public override string VersionString { get; } = "1.3.0";
        private readonly string _rustParserToolPath;

        public RustLanguageService(IConfiguration configuration, TelemetryClient telemetryClient) : base(telemetryClient)
        {
            _rustParserToolPath = configuration["RustParserToolPath"];
        }
        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonFilePath)
        {
            var jsPath = Path.Combine(_rustParserToolPath, "main.js");
            return $"{jsPath} \"{originalName}\" \"{jsonFilePath}\"";
        }
    }
}

