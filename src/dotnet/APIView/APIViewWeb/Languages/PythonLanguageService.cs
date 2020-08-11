// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class PythonLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "Python";
        public override string Extension { get; } = ".whl";
        public override string VersionString { get; } = "0.1.3";

        private readonly string _python3ProcessName;
        public override string ProcessName => _python3ProcessName;

        public PythonLanguageService(IConfiguration configuration)
        {
            // Default python version in azure web app is py 2.7 and python3 is not used by default
            // when running script using python process.
            // We need to run the script using full path to python3
            _python3ProcessName = configuration["PYTHON3HOME"] ?? "python";
        }
        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonPath)
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
