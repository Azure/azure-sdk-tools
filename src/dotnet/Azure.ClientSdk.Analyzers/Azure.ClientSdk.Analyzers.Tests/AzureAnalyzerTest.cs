// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Azure.ClientSdk.Analyzers.Tests 
{
    public class AzureAnalyzerTest<TAnalyzer> : CSharpAnalyzerTest<TAnalyzer, XUnitVerifier> where TAnalyzer : DiagnosticAnalyzer, new() 
    {
        public AzureAnalyzerTest(LanguageVersion languageVersion = LanguageVersion.Latest) 
        {
            SolutionTransforms.Add((solution, projectId) =>
            {
                var project = solution.GetProject(projectId);
                var parseOptions = (CSharpParseOptions)project.ParseOptions;
                return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(languageVersion));
            });

            TestState.AdditionalReferences.Add(typeof(ValueTask).Assembly);
            TestState.AdditionalReferences.Add(typeof(IAsyncDisposable).Assembly);
        }

        public string DescriptorName { get; set; }

        protected override DiagnosticDescriptor GetDefaultDiagnostic(DiagnosticAnalyzer[] analyzers) 
            => string.IsNullOrWhiteSpace(DescriptorName) 
                ? base.GetDefaultDiagnostic(analyzers)
                : analyzers.SelectMany(a => a.SupportedDiagnostics).FirstOrDefault(d => d.Id == DescriptorName) ?? base.GetDefaultDiagnostic(analyzers);
    }
}