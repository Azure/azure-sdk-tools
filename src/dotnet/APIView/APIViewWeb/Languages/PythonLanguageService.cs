// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;

namespace APIViewWeb
{
    public class PythonLanguageService : LanguageProcessor
    {
        private static readonly string PythonHome = Environment.GetEnvironmentVariable("APIVIEW_PYTHONHOME") ?? string.Empty;

        public override string Name { get; } = "Python";
        public override string Extension { get; } = ".whl";
        public override string ProcessName { get; } = Path.Combine(PythonHome, "Scripts", "apistubgen");
        public override string VersionString { get; } = "0.1.1";

        public override string GetProccessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            return $"--pkg-path {originalName} --temp-path {tempDirectory}" +
                $" --out-path {jsonPath} --hide-report";
        }
    }
}
