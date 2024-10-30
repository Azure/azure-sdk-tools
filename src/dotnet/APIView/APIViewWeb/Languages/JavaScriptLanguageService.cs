// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class JavaScriptLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "JavaScript";
        public override string[] Extensions { get; } = { ".api.json" };
        public override string ProcessName { get; } = "node";
        public override string VersionString { get; } = "2.0.3";
        private readonly string _jsParserToolPath;

        public JavaScriptLanguageService(IConfiguration configuration, TelemetryClient telemetryClient) : base(telemetryClient)
        {
            _jsParserToolPath = configuration["JavaScriptParserToolPath"];
        }
        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonFilePath)
        {
            var jsPath = Path.Combine(_jsParserToolPath, "export.js");
            return $"{jsPath} \"{originalName}\" \"{jsonFilePath}\"";
        }
    }
}
