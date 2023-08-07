// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing.XUnit;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public static class AzureAnalyzerVerifier<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer, new()
    {
        private static readonly ReferenceAssemblies DefaultReferenceAssemblies =
            ReferenceAssemblies.Default.AddPackages(ImmutableArray.Create(
                new PackageIdentity("Azure.Core", "1.26.0"),
                new PackageIdentity("Microsoft.Bcl.AsyncInterfaces", "1.1.0"),
                new PackageIdentity("Newtonsoft.Json", "12.0.3"),
                new PackageIdentity("System.Text.Json", "4.6.0"),
                new PackageIdentity("System.Threading.Tasks.Extensions", "4.5.3")));

        public static CSharpAnalyzerTest<TAnalyzer, XUnitVerifier> CreateAnalyzer(string source, LanguageVersion languageVersion = LanguageVersion.Latest)
            => new CSharpAnalyzerTest<TAnalyzer, XUnitVerifier>
            {
                ReferenceAssemblies = DefaultReferenceAssemblies,
                SolutionTransforms = {(solution, projectId) =>
                {
                    var project = solution.GetProject(projectId);
                    var parseOptions = (CSharpParseOptions)project.ParseOptions;
                    return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(languageVersion));
                }},
                TestCode = source,
                TestBehaviors = TestBehaviors.SkipGeneratedCodeCheck
            };

        public static Task VerifyAnalyzerAsync(string source, LanguageVersion languageVersion = LanguageVersion.Latest)
            => CreateAnalyzer(source, languageVersion).RunAsync(CancellationToken.None);

        public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] diagnostics)
        {
            var test = CreateAnalyzer(source);
            test.ExpectedDiagnostics.AddRange(diagnostics);
            return test.RunAsync(CancellationToken.None);
        }

        public static Task VerifyAnalyzerAsync(string source, List<(string fileName, string source)> files)
        {
            var test = CreateAnalyzer(source);
            foreach (var file in files)
            {
                test.TestState.Sources.Add(file);
            }
            return test.RunAsync(CancellationToken.None);
        }

        public static DiagnosticResult Diagnostic(string expectedDescriptor) => AnalyzerVerifier<TAnalyzer>.Diagnostic(expectedDescriptor);
    }
}
