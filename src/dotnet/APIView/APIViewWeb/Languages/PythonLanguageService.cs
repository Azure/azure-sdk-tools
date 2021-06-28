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
        public override string VersionString { get; } = "0.2.3";

        private readonly string _apiViewPythonProcessor;
        public override string ProcessName => _apiViewPythonProcessor;

        public PythonLanguageService(IConfiguration configuration)
        {
            // apistubgen is located in python's scripts path e.g. <Pythonhome>/Scripts/apistubgen
            // Env variable PYTHONPROCESSORPATH is set to <pythonhome>/Scripts/apistubgen where parser is located
            _apiViewPythonProcessor = configuration["PYTHONPROCESSORPATH"] ?? string.Empty;
        }
        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            return $"--pkg-path {originalName} --temp-path {tempDirectory}" +
                $" --out-path {jsonPath} --hide-report";
        }
    }
}
