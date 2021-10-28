// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ApiView;

namespace APIViewWeb
{
    public class CSharpLanguageService : LanguageService
    {
        private static Regex _packageNameParser = new Regex("([A-Za-z.]*[a-z]).([\\S]*)", RegexOptions.Compiled);
        public override string Name { get; } = "C#";
        public override string Extension { get; } = ".dll";

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
            return versionString != CodeFileBuilder.CurrentVersion;
        }

        public override Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            ZipArchive archive = null;
            try
            {
                Stream dllStream = stream;
                Stream docStream = null;

                if (IsNuget(originalName))
                {
                    archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
                    foreach (var entry in archive.Entries)
                    {
                        if (IsDll(entry.Name))
                        {
                            dllStream = entry.Open();
                            var docEntry = archive.GetEntry(Path.ChangeExtension(entry.FullName, ".xml"));
                            if (docEntry != null)
                            {
                                docStream = docEntry.Open();
                            }
                            break;
                        }
                    }
                }

                var assemblySymbol = CompilationFactory.GetCompilation(dllStream, docStream);
                if ( assemblySymbol == null)
                {
                    return Task.FromResult(GetDummyReviewCodeFile(originalName));
                }
                
                return Task.FromResult(new CodeFileBuilder().Build(assemblySymbol, runAnalysis));
            }
            finally
            {
                archive?.Dispose();
            }
        }

        private CodeFile GetDummyReviewCodeFile(string originalName)
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

            return new CodeFile()
            {                
                Name = reviewName,
                Language = "C#",
                VersionString = CodeFileBuilder.CurrentVersion,
                PackageName = packageName
            };
        }
    }
}