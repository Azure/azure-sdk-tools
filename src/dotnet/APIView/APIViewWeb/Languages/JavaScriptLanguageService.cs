// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

namespace APIViewWeb
{
    public class JavaScriptLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "JavaScript";
        public override string Extension { get; } = ".api.json";
        public override string ProcessName { get; } = "node";
        public override string VersionString { get; } = "1";

        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonFilePath)
        {
            var jsPath = Path.Combine(
                    Path.GetDirectoryName(typeof(JavaScriptLanguageService).Assembly.Location),
                    "export.js");

            return $"{jsPath} \"{originalName}\" \"{jsonFilePath}\"";
        }
    }
}
