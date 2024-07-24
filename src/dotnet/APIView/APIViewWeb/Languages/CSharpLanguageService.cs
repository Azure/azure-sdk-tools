// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ApiView;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class CSharpLanguageService : LanguageProcessor
    {
        private readonly string _csharpParserToolPath;
        private static Regex _packageNameParser = new Regex("([A-Za-z.]*[a-z]).([\\S]*)", RegexOptions.Compiled);
        public override string Name { get; } = "C#";
        public override string[] Extensions { get; } = { ".dll" };
        public override string ProcessName => _csharpParserToolPath;
        public override string VersionString { get; } = "28";

        public CSharpLanguageService(IConfiguration configuration, TelemetryClient telemetryClient) : base(telemetryClient)
        {
            _csharpParserToolPath = configuration["CSHARPPARSEREXECUTABLEPATH"];
        }

        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            var outputFileName = Path.GetFileName(jsonPath).Replace(".json.tgz", "");
            return $"--packageFilePath \"{originalName}\" --outputDirectoryPath \"{tempDirectory}\" --outputFileName \"{outputFileName}\"";
        }

        public override bool IsSupportedFile(string name)
        {
            return IsDll(name) || IsNuget(name);
        }

        private static bool IsDll(string name)
        {
            return name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNuget(string name)
        {
            return name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase);
        }

        public override bool CanUpdate(string versionString)
        {
            return versionString != VersionString;
        }

        private CodeFile GetDummyReviewCodeFile(string originalName, List<DependencyInfo> dependencies)
        {
            var packageName = Path.GetFileNameWithoutExtension(originalName);
            var reviewName = "";
            var packageNameMatch = _packageNameParser.Match(packageName);
            if (packageNameMatch.Success)
            {
                packageName = packageNameMatch.Groups[1].Value;
                reviewName = $"{packageName} ({packageNameMatch.Groups[2].Value})";
            }
            else
            {
                reviewName = $"{packageName} (metapackage)";
            }

            var builder = new CodeFileTokensBuilder();
            CodeFileBuilder.BuildDependencies(builder, dependencies);

            return new CodeFile()
            {
                Name = reviewName,
                Language = "C#",
                VersionString = CodeFileBuilder.CurrentVersion,
                PackageName = packageName,
                Tokens = builder.Tokens.ToArray()
            };
        }
    }
}
