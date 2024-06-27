// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ApiView;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace APIViewWeb
{
    public class CSharpLanguageService : LanguageProcessor
    {
        private readonly string _csharpParserToolPath;
        private static Regex _packageNameParser = new Regex("([A-Za-z.]*[a-z]).([\\S]*)", RegexOptions.Compiled);
        public override string Name { get; } = "C#";
        public override string[] Extensions { get; } = { ".dll" };
        public override string ProcessName => _csharpParserToolPath;
        public override string VersionString { get; } = "27";

        public CSharpLanguageService(IConfiguration configuration, TelemetryClient telemetryClient)
        {
            _csharpParserToolPath = configuration["CSHARPPARSEREXECUTABLEPATH"];
        }

        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            var outputFileName = Path.GetFileName(jsonPath).Replace(".json.tgz", "");
            return $"--packageFilePath \"{originalName}\" --outputDirectoryPath \"{tempDirectory}\" --outputFileName \"{outputFileName}\"";
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

        /// <summary>
        /// Resolves the NuGet package dependencies and extracts them to a temporary folder. It is the responsibility of teh caller to clean up the folder.
        /// </summary>
        /// <param name="dependencyInfos">The dependency infos</param>
        /// <returns>A temporary path where the dependency files were extracted.</returns>
        private async Task<string> ExtractNugetDependencies(List<DependencyInfo> dependencyInfos)
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            SourceCacheContext cache = new SourceCacheContext();
            SourceRepository repository = NuGet.Protocol.Core.Types.Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            try
            {
                FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>().ConfigureAwait(false);
                foreach (var dep in dependencyInfos)
                {
                    using (MemoryStream packageStream = new MemoryStream())
                    {
                        if (await resource.CopyNupkgToStreamAsync(
                        dep.Name,
                        new NuGetVersion(dep.Version),
                        packageStream,
                        cache,
                        NullLogger.Instance,
                        CancellationToken.None))
                        {
                            using PackageArchiveReader reader = new PackageArchiveReader(packageStream);
                            NuspecReader nuspec = reader.NuspecReader;
                            var file = reader.GetFiles().FirstOrDefault(f => f.EndsWith(dep.Name + ".dll"));
                            if (file != null)
                            {
                                var fileInfo = new FileInfo(file);
                                var path = Path.Combine(tempFolder, dep.Name, fileInfo.Name);
                                var tmp = reader.ExtractFile(file, path, NullLogger.Instance);
                            }
                        }
                    }
                }
            }
            finally
            {
                cache.Dispose();
            }
            return tempFolder;
        }
    }
}
