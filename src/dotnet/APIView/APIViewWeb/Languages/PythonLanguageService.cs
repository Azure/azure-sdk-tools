// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

namespace APIViewWeb
{
    public class PythonLanguageService : LanguageProcessor
    {
        private static readonly string PythonHome = Environment.GetEnvironmentVariable("PYTHONHOME") ?? string.Empty;

        public override string Name { get; } = "Python";
        public override string Extension { get; } = ".whl";
        public override string ProcessName { get; } = Path.Combine(PythonHome, "python");
        public override string VersionString { get; } = "0.1.1";

        public override string GetProccessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            var pythonScriptPath = Path.Combine(
                    Path.GetDirectoryName(typeof(PythonLanguageService).Assembly.Location),
                    "api-stub-generator",
                    "apistubgen.py"
                    );
            return $"{pythonScriptPath} --pkg-path {originalName} --temp-path {tempDirectory}" +
                $" --out-path {jsonPath} --hide-report";
        }
    }
}
