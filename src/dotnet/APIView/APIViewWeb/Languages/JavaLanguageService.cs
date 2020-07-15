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
        public override string Extension { get; } = ".jar";
        public override string ProcessName { get; } = "java";
        public override string VersionString { get; } = "apiview-java-processor-1.9.0.jar";

        public override string GetProccessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            var jarPath = Path.Combine(
                    Path.GetDirectoryName(typeof(JavaLanguageService).Assembly.Location),
                    VersionString);
            return $"-jar {jarPath} \"{originalName}\" \"{tempDirectory}\"";
        }

    }
}
