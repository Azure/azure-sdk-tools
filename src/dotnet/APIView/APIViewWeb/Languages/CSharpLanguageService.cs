// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using ApiView;

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

        public override Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            ZipArchive archive = null;
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

                var assemblySymbol = CompilationFactory.GetCompilation(dllStream, docStream);
                if ( assemblySymbol == null)
                {
                    return Task.FromResult(GetDummyReviewCodeFile(originalName, dependencies));
                }

                return Task.FromResult(new CodeFileBuilder().Build(assemblySymbol, runAnalysis, dependencies));
            }
            finally
            {
                archive?.Dispose();
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
    }
}
