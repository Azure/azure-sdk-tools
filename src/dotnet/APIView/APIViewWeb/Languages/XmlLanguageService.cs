// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;

namespace APIViewWeb
{
    public class XmlLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "Xml";
        public override string[] Extensions { get; } = { ".xml" };
        public override string ProcessName { get; } = "java";
        public override string VersionString { get; } = "apiview-java-processor-1.31.0.jar";

        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            var jarPath = Path.Combine(
                    Path.GetDirectoryName(typeof(XmlLanguageService).Assembly.Location),
                    VersionString);
            return $"-jar {jarPath} \"{originalName}\" \"{tempDirectory}\"";
        }

    }
}
