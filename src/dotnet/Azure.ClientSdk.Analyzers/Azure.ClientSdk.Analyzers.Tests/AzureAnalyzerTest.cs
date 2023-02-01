// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AzureAnalyzerTest<TAnalyzer> : CSharpAnalyzerTest<TAnalyzer, XUnitVerifier> where TAnalyzer : DiagnosticAnalyzer, new()
    {
        private static readonly ReferenceAssemblies DefaultReferenceAssemblies =
            ReferenceAssemblies.Default.AddPackages(ImmutableArray.Create(
                new PackageIdentity("Azure.Core", "1.26.0"),
                new PackageIdentity("Microsoft.Bcl.AsyncInterfaces", "1.1.0"),
                new PackageIdentity("System.Text.Json", "4.6.0"),
                new PackageIdentity("Newtonsoft.Json", "12.0.3"),
                new PackageIdentity("System.Threading.Tasks.Extensions", "4.5.3")));

        public AzureAnalyzerTest(LanguageVersion languageVersion = LanguageVersion.Latest)
        {
            SolutionTransforms.Add((solution, projectId) =>
            {
                var project = solution.GetProject(projectId);
                var parseOptions = (CSharpParseOptions)project.ParseOptions;
                return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(languageVersion));
            });

            ReferenceAssemblies = DefaultReferenceAssemblies;
        }
    }
}
