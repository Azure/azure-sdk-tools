// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using ApiView;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace APIViewWeb
{
    public class CSharpLanguageService : LanguageService
    {
        private static Regex _packageNameParser = new Regex("([A-Za-z.]*[a-z]).([\\S]*)", RegexOptions.Compiled);
        public override string Name { get; } = "C#";
        public override string[] Extensions { get; } = { ".dll" };

        public override bool IsSupportedFile(string name)
        {
            return IsDll(name) || IsNuget(name);
        }

        private static bool IsDll(string name)
        {
            return name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNuspec(string name)
        {
            return name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNuget(string name)
        {
            return name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase);
        }

        public override bool CanUpdate(string versionString)
        {
            return versionString != CodeFileBuilder.CurrentVersion;
        }

        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            ZipArchive archive = null;
            string dependencyFilesTempDir = null;
            try
            {
                Stream dllStream = stream;
                Stream docStream = null;
                List<DependencyInfo> dependencies = null;

                if (IsNuget(originalName))
                {
                    archive = new ZipArchive(stream, ZipArchiveMode.Read, true);

                    var nuspecEntry = archive.Entries.Single(entry => IsNuspec(entry.Name));

                    var dllEntries = archive.Entries.Where(entry => IsDll(entry.Name)).ToArray();
                    if (dllEntries.Length == 0)
                    {
                        return GetDummyReviewCodeFile(originalName, dependencies);
                    }

                    var dllEntry = dllEntries.First();
                    if (dllEntries.Length > 1)
                    {
                        // If there are multiple dlls in the nupkg (e.g. Cosmos), try to find the first that matches the nuspec name, but
                        // fallback to just using the first one.
                        dllEntry = dllEntries.FirstOrDefault(
                            dll => Path.GetFileNameWithoutExtension(nuspecEntry.Name)
                                .Equals(Path.GetFileNameWithoutExtension(dll.Name), StringComparison.OrdinalIgnoreCase)) ?? dllEntry;
                    }

                    dllStream = dllEntry.Open();
                    var docEntry = archive.GetEntry(Path.ChangeExtension(dllEntry.FullName, ".xml"));
                    if (docEntry != null)
                    {
                        docStream = docEntry.Open();
                    }
                    using var nuspecStream = nuspecEntry.Open();
                    var document = XDocument.Load(nuspecStream);
                    var dependencyElements = document.Descendants().Where(e => e.Name.LocalName == "dependency");
                    dependencies = new List<DependencyInfo>();
                    dependencies.AddRange(
                            dependencyElements.Select(dependency => new DependencyInfo(
                                    dependency.Attribute("id").Value,
                                        dependency.Attribute("version").Value)));
                    // filter duplicates and sort
                    if (dependencies.Any())
                    {
                        dependencies = dependencies
                        .GroupBy(d => d.Name)
                        .Select(d => d.First())
                        .OrderBy(d => d.Name).ToList();
                    }
                }

                dependencyFilesTempDir = await ExtractNugetDependencies(dependencies).ConfigureAwait(false);
                var dependencyFilePaths = Directory.EnumerateFiles(dependencyFilesTempDir, "*.dll", SearchOption.AllDirectories);

                var assemblySymbol = CompilationFactory.GetCompilation(dllStream, docStream, dependencyFilePaths);
                if (assemblySymbol == null)
                {
                    return GetDummyReviewCodeFile(originalName, dependencies);
                }

                return new CodeFileBuilder().Build(assemblySymbol, runAnalysis, dependencies);
            }
            finally
            {
                archive?.Dispose();
                if (dependencyFilesTempDir != null && Directory.Exists(dependencyFilesTempDir))
                {
                    Directory.Delete(dependencyFilesTempDir, true);
                }
            }
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
            return tempFolder;
        }
    }
}
