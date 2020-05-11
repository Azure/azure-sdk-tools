// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class PythonLanguageService : LanguageProcessor
    {
        private readonly string ApiViewPythonProcessor;
        public override string Name { get; } = "Python";
        public override string Extension { get; } = ".whl";
        public override string ProcessName
        {
            get{ return ApiViewPythonProcessor; }
        }
        public override string VersionString { get; } = "0.1.1";

        public PythonLanguageService(IConfiguration configuration)
        {
            // apistubgen is located in python's scripts path e.g. <Pythonhome>/Scripts
            var PythonHome = configuration["APIVIEW_PYTHONHOME"] ?? string.Empty;
            ApiViewPythonProcessor = Path.Combine(PythonHome, "Scripts", "apistubgen");
        }

        public override string GetProccessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            return $"--pkg-path {originalName} --temp-path {tempDirectory}" +
                $" --out-path {jsonPath} --hide-report";
        }
    }
}
