// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Sdk.Tools.Cli.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Azure.Sdk.Tools.Cli.SampleGeneration;

/// <summary>
/// Discovers C# source and supporting infrastructure files for .NET client libraries.
/// </summary>
public class DotNetSourceInputProvider : ILanguageSourceInputProvider
{

    private static readonly string[] s_testFileNameIndicators =
    [
        "Tests.cs",
        "Test.cs",
        "Facts.cs",
        "Spec.cs",
    ];

    private static readonly HashSet<string> s_testMethodAttributes = new(StringComparer.OrdinalIgnoreCase)
    { "Test", "Fact", "Theory", "TestCase", "DataTestMethod", "Repeat" };

    private static readonly HashSet<string> s_lifecycleAttributes = new(StringComparer.OrdinalIgnoreCase)
    { "SetUp", "TearDown", "OneTimeSetUp", "OneTimeTearDown", "ClassInitialize", "ClassCleanup", "AssemblyInitialize", "AssemblyCleanup" };

    private static readonly HashSet<string> s_infraBaseTypeIndicators = new(StringComparer.OrdinalIgnoreCase)
    { "RecordedTestBase", "ClientTestBase", "SamplesBase" };

    private static readonly ConcurrentDictionary<string, (DateTime LastWrite, bool IsTest)> s_cache = new();

    public IReadOnlyList<SourceInput> Create(string packagePath)
    {
        var result = new List<SourceInput>();

        var src = Path.Combine(packagePath, "src");
        if (Directory.Exists(src))
        {
            result.Add(new SourceInput(src, IncludeExtensions: [".cs"]));
        }

        var tests = Path.Combine(packagePath, "tests");
        if (Directory.Exists(tests))
        {
            foreach (var file in EnumerateInfrastructureFiles(tests))
            {
                result.Add(new SourceInput(file));
            }
        }

        var samples = Path.Combine(packagePath, "tests", "samples");
        if (Directory.Exists(samples))
        {
            result.Add(new SourceInput(samples, IncludeExtensions: [".cs"]));
        }

        return result;
    }

    private static IEnumerable<string> EnumerateInfrastructureFiles(string testsDirectory)
    {
        foreach (var file in Directory.EnumerateFiles(testsDirectory, "*.cs", SearchOption.AllDirectories))
        {
            if (!IsTestFile(file))
            {
                yield return file;
            }
        }
    }

    private static bool IsTestFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        foreach (var suffix in s_testFileNameIndicators)
        {
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        try
        {
            var info = new FileInfo(filePath);
            if (s_cache.TryGetValue(info.FullName, out var cached) && cached.LastWrite == info.LastWriteTimeUtc)
            {
                return cached.IsTest;
            }

            var text = File.ReadAllText(filePath);
            if (s_testMethodAttributes.Any(a => text.Contains("[" + a, StringComparison.Ordinal)))
            {
                s_cache[info.FullName] = (info.LastWriteTimeUtc, true);
                return true;
            }

            var tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetRoot();
            bool anyTest = false;
            bool anyLifecycleOnly = false;
            bool anyAbstract = false;
            bool inheritsInfra = false;

            foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (cls.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
                {
                    anyAbstract = true;
                }

                if (cls.BaseList != null)
                {
                    foreach (var t in cls.BaseList.Types)
                    {
                        var baseName = t.Type.ToString().Split('<')[0];
                        if (s_infraBaseTypeIndicators.Contains(baseName))
                        {
                            inheritsInfra = true;
                            break;
                        }
                    }
                }
                foreach (var method in cls.Members.OfType<MethodDeclarationSyntax>())
                {
                    var attrs = method.AttributeLists.SelectMany(a => a.Attributes).Select(a => a.Name.ToString().Split('.').Last());
                    if (attrs.Any(a => s_testMethodAttributes.Contains(a))) { anyTest = true; break; }
                    if (attrs.Any(a => s_lifecycleAttributes.Contains(a)))
                    {
                        anyLifecycleOnly = true;
                    }
                }
                if (anyTest)
                {
                    break;
                }
            }

            bool isTest = anyTest;
            if (!isTest && (anyAbstract || inheritsInfra || anyLifecycleOnly))
            {
                isTest = false;
            }
            s_cache[info.FullName] = (info.LastWriteTimeUtc, isTest);
            return isTest;
        }
        catch
        {
            try
            {
                foreach (var line in File.ReadLines(filePath))
                {
                    if (s_testMethodAttributes.Any(a => line.Contains("[" + a, StringComparison.Ordinal)))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
