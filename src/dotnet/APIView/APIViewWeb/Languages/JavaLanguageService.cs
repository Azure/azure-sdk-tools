// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public class JavaLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "Java";

        public string JarName = "apiview-java-processor-1.4.0.jar";

        public override bool IsSupportedExtension(string extension)
        {
            return string.Equals(extension, ".jar", comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        public override string GetProccessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            var jarPath = Path.Combine(
                    Path.GetDirectoryName(typeof(JavaLanguageService).Assembly.Location),
                    JarName);
            return $"-jar {jarPath} \"{originalName}\" \"{tempDirectory}\"";
        }

        public override string GetLanguage()
        {
            return "Java";
        }

        public override string GetProcessName()
        {
            return "java";
        }

        public override string GetVersionString()
        {
            return JarName;
        }
    }
}
