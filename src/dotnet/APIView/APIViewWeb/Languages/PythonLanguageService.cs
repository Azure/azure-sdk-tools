// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public class PythonLanguageService : LanguageProcessor
    {
        public string pythonParserVersion = "0.1.1";

        public override string Name { get; } = "Python";

        public override bool IsSupportedExtension(string extension)
        {
            return string.Equals(extension, ".whl", comparisonType: StringComparison.OrdinalIgnoreCase);
        }
        
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

        public override string GetLanguage()
        {
            return "Python";
        }

        public override string GetProcessName()
        {
            return "python";
        }

        public override string GetVersionString()
        {
            return pythonParserVersion;
        }

    }
}
